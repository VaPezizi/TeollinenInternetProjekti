import express, { json } from 'express';
import cors from 'cors';
import { createServer } from 'http';
import { Server } from 'socket.io';
import mongoose from 'mongoose';
import dotenv from 'dotenv';
import mqtt from 'mqtt';

dotenv.config();

let options = {
  host: process.env.MQTT_HOST,
  port: parseInt(process.env.MQTT_PORT),
  username: process.env.MQTT_USERNAME,
  password: process.env.MQTT_PASSWORD,
  protocol: "mqtts",
  protocolVersion: 5,
}

const mongoURI = process.env.MONGODB_URI;
console.log("MongoDB URI from env: ", mongoURI);
console.log("Connecting to MongoDB at ", mongoURI);
console.log(options);


mongoose.set('strictQuery', false);
mongoose.connect(mongoURI)

const measurementsSchema = new mongoose.Schema({
  x: Number,
  y: Number,
  sw: Number,
  pot: Number,
  urm: Number,
  timestamp: { type: Date, default: Date.now }
});

const Measurement = mongoose.model('Measurement', measurementsSchema);

console.log("MQTT connection options: ", options);

let client = mqtt.connect(options);
client.on('connect', (connack) => {
  console.log('MQTT connected', connack)
})
client.on('reconnect', (connack) => {
  console.log('MQTT reconnecting', connack)
})
client.on('error', (err) => {
  console.error('MQTT error', err)
})

const app = express();
const server = createServer(app);
const io = new Server(server, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});
app.use(cors());
app.use(express.json())

let lista = []
/*
const mongoURI = process.env.MONGO_URI;
console.log("Connecting to MongoDB at ", mongoURI);
mongoose.set('strictQuery', false);
mongoose.connect(mongoURI)

/*
typedef struct {
  int jx_value,
      jy_value,
      jsw_value;
  double potentiometer_value,
      urm_distance;
} DeviceState;
*/

app.delete('/api/data/:id', async (request, response) => {
  try {
    console.log("Deleting measurement with id: ", request.params.id);
    if (!mongoose.Types.ObjectId.isValid(request.params.id)) {
      return response.status(400).json({
        success: false,
        message: 'Invalid measurement ID'
      });
    }
    const deleted = await Measurement.deleteOne({ _id: (request.params.id) });
    if (deleted.deletedCount === 0) {
      return response.status(404).json({
        success: false,
        message: 'Measurement not found'
      });
    }
    response.status(200).json({
      success: true,
      message: 'Measurement deleted'
    });
  } catch (error) {
    response.status(500).json({
      success: false,
      message: error.message
    });
  }
});

app.get('/api/measurements', async (request, response) => {
  /*response.json({
    success: true,
    data: measurements,
    count: measurements.length
  })
  */
  try {   
    const measurements = await Measurement.find();
    response.status(200).json({
      measurements
    })
  }
  catch (error) {
    response.status(500).json({
      success: false,
      message: error.message
    })
  }
  
});

app.post('/api/data', async (request, response) => {
  //console.log("request body ", request.body);
  try {
    const p = request.body;
    console.log(p)
    /*
    if ((p.height === undefined || p.lon === undefined || p.lat === undefined)) {
      return response.status(400).json({
        success: false,
        message: 'Invalid location data'
      })
    }*/
    if(p.x == undefined || p.y == undefined || p.sw == undefined || p.pot == undefined || p.urm == undefined){
      return response.status(400).json({
        success: false,
        message: 'Invalid data'
      })
    }
    

    console.log("request body content", request.body);
    
    /*const newMeasurement = {
        y: p.y, // x,y axis of joystick
        x: p.x, 
        sw: p.sw,   //Joystick switch (pressed or not)
        pot: p.pot,     //Potentiometer value
        urm: p.urm      //Urm distance
    };
    lista.push(newMeasurement);*/
    const newMeasurement = new Measurement({
      y: p.y, // x,y axis of joystick
      x: p.x,
      sw: p.sw,   //Joystick switch (pressed or not)
      pot: p.pot,     //Potentiometer value
      urm: p.urm      //Urm distance

    });
    const savedMeasurement = await newMeasurement.save();

    const payload = JSON.stringify(savedMeasurement);
    if (!client.connected) {
      console.warn('MQTT client not connected — publish may fail');
    }
    console.log('Publishing to MQTT topic teollinen/data with payload:', payload);
    client.publish('teollinen/data', payload, { qos: 1 }, (err) => {
      if (err) {
        console.error('MQTT publish failed:', err);
        return response.status(500).json({
          success: false,
          message: 'MQTT publish failed',
          error: err && err.message
        });
      }

      console.log('MQTT publish acknowledged for topic teollinen/data');
      return response.status(201).json({
        success: true,
        data: savedMeasurement
      });
    });
  } catch (error) {
    return response.status(500).json({
      success: false,
      message: error.message
    });
  }

})

const PORT = 3001
server.listen(PORT, () => {
  console.log(`Server running on port ${PORT}`)
})
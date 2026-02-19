import express, { json } from 'express';
import cors from 'cors';
import { createServer } from 'http';
import { Server } from 'socket.io';
//import mongoose from 'mongoose';
import dotenv from 'dotenv';
import mqtt from 'mqtt';

dotenv.config();

let options = {
  host: process.env.MQTT_HOST,
  port: parseInt(process.env.MQTT_PORT),
  username: process.env.MQTT_USERNAME,
  password: process.env.MQTT_PASSWORD
}

let client = mqtt.connect(options);

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


app.get('/api/measurements', async (request, response) => {
  /*response.json({
    success: true,
    data: measurements,
    count: measurements.length
  })
  */
  return response.status(200).json(lista)
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
    const newMeasurement = {
        y: p.y, // x,y axis of joystick
        x: p.x, 
        sw: p.sw,   //Joystick switch (pressed or not)
        pot: p.pot,     //Potentiometer value
        urm: p.urm      //Urm distance
    };
    lista.push(newMeasurement);

    client.publish('teollinen/data', JSON.stringify(newMeasurement));
    
    return response.status(201).json({
      success: true,
      data: newMeasurement
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
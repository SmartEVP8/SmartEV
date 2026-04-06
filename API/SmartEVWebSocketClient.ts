/**
 * Example frontend client for connecting to SmartEV WebSocket API
 * 
 * Prerequisites:
 * - npm install protobufjs
 * 
 * Usage:
 * const client = new SmartEVWebSocketClient('ws://localhost:5000/ws/simulation');
 * client.connect();
 * client.on('snapshot', (snapshot) => console.log('State:', snapshot));
 */

// TODO: DELETE THIS FILE ONCE FRONTEND STRUCTURE IS FINALIZED

import * as protobuf from 'protobufjs';

export class SmartEVWebSocketClient {
  private ws: WebSocket | null = null;
  private url: string;
  private proto: protobuf.Root | null = null;
  private listeners: Map<string, Function[]> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  constructor(url: string) {
    this.url = url;
  }

  /**
   * Connect to the WebSocket and load protobuf schema
   */
  async connect(): Promise<void> {
    try {
      // Load protobuf schema
      this.proto = await protobuf.load('/api.proto');

      // Create WebSocket connection
      this.ws = new WebSocket(this.url);
      this.ws.binaryType = 'arraybuffer';

      this.ws.onopen = () => {
        console.log('Connected to SmartEV simulation');
        this.reconnectAttempts = 0;
        this.emit('connected');
      };

      this.ws.onmessage = (event) => {
        this.handleMessage(event.data as ArrayBuffer);
      };

      this.ws.onerror = (error) => {
        console.error('WebSocket error:', error);
        this.emit('error', error);
      };

      this.ws.onclose = () => {
        console.log('Disconnected from SmartEV simulation');
        this.emit('disconnected');
        this.attemptReconnect();
      };
    } catch (error) {
      console.error('Failed to connect:', error);
      throw error;
    }
  }

  /**
   * Disconnect from WebSocket
   */
  disconnect(): void {
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
  }

  /**
   * Handle incoming binary protobuf messages
   */
  private handleMessage(data: ArrayBuffer): void {
    try {
      const uint8Array = new Uint8Array(data);

      // Try to decode as different message types
      // In a real implementation, you'd have a message type header
      
      // Try StateSnapShot
      const StateSnapShot = this.proto?.lookupType('SmartEV.StateSnapShot');
      if (StateSnapShot) {
        try {
          const snapshot = StateSnapShot.decode(uint8Array);
          this.emit('snapshot', snapshot.toJSON());
          return;
        } catch (e) {
          // Try next type
        }
      }

      // Try ArriveAtStation
      const ArriveAtStation = this.proto?.lookupType('SmartEV.ArriveAtStation');
      if (ArriveAtStation) {
        try {
          const arrival = ArriveAtStation.decode(uint8Array);
          this.emit('arrival', arrival.toJSON());
          return;
        } catch (e) {
          // Try next type
        }
      }

      // Try EndCharging
      const EndCharging = this.proto?.lookupType('SmartEV.EndCharging');
      if (EndCharging) {
        try {
          const charging = EndCharging.decode(uint8Array);
          this.emit('charging-end', charging.toJSON());
          return;
        } catch (e) {
          // Try next type
        }
      }

      // Try RequestStationState
      const RequestStationState = this.proto?.lookupType('SmartEV.RequestStationState');
      if (RequestStationState) {
        try {
          const state = RequestStationState.decode(uint8Array);
          this.emit('station-state', state.toJSON());
          return;
        } catch (e) {
          // Try next type
        }
      }

      // Try Init
      const Init = this.proto?.lookupType('SmartEV.Init');
      if (Init) {
        try {
          const init = Init.decode(uint8Array);
          this.emit('init', init.toJSON());
          return;
        } catch (e) {
          // Unknown message type
        }
      }

      console.warn('Could not decode message');
    } catch (error) {
      console.error('Error handling message:', error);
    }
  }

  /**
   * Register event listener
   */
  on(event: string, callback: Function): void {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, []);
    }
    this.listeners.get(event)!.push(callback);
  }

  /**
   * Emit event to listeners
   */
  private emit(event: string, data?: any): void {
    const callbacks = this.listeners.get(event) || [];
    callbacks.forEach((callback) => {
      try {
        callback(data);
      } catch (error) {
        console.error(`Error in ${event} listener:`, error);
      }
    });
  }

  /**
   * Attempt to reconnect
   */
  private attemptReconnect(): void {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      this.reconnectAttempts++;
      const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);
      console.log(`Attempting to reconnect in ${delay}ms...`);
      setTimeout(() => this.connect(), delay);
    } else {
      console.error('Max reconnection attempts reached');
      this.emit('max-reconnect-attempts-reached');
    }
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.ws?.readyState === WebSocket.OPEN;
  }
}

// Example usage:
/*
const client = new SmartEVWebSocketClient('ws://localhost:5000/ws/simulation');

client.on('connected', () => {
  console.log('Ready to receive data');
});

client.on('snapshot', (snapshot) => {
  console.log('Total EVs:', snapshot.totalEVs);
  console.log('Total Charging:', snapshot.totalCharging);
});

client.on('arrival', (arrival) => {
  console.log('EV arrived at station:', arrival.stationId, 'at time:', arrival.time);
});

client.on('charging-end', (charging) => {
  console.log('Charging ended at station:', charging.stationId);
});

client.on('station-state', (state) => {
  console.log('Station state received:', state);
});

client.on('error', (error) => {
  console.error('Connection error:', error);
});

await client.connect();
*/

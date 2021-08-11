import { OpCodes } from "./models/OpCodes";
import { SocketFrame } from "./models/SocketFrame";

export class WebsocketClient {
    public isConnected: boolean;
    public onOpen: (resumed: boolean) => void;
    public onClose: (reason?: string) => void;


    private socket?: WebSocket;

    private url: string;

    private listeners: { event: string, listener: (data: any) => void }[] = [];

    constructor(url: string) {
        this.isConnected = false;
        this.url = url;
    }

    public async connect(auth: string, page: string, events: string[], timeout: number = 5000): Promise<boolean> {
        const socket = new WebSocket(`${this.url}`);

        return await new Promise<boolean>((resolve, reject) => {
            if (timeout > 0) {
                setTimeout(() => {
                    resolve(false);
                }, timeout);
            }

            socket.onopen = async (ev) => {
                this.socket = socket;
                this.isConnected = true;

                // start the handshake.
                
                await this.sendHandshake(auth, events, page);

                socket.onmessage = (ev) => {
                    var msg = JSON.parse(ev.data) as SocketFrame;

                    if (msg.op === OpCodes.HandshakeResult) {
                        this.isConnected = true;

                        if (this.onOpen)
                            this.onOpen(msg.d.resumed);
                        
                        socket.onmessage = (ev) => this.handleMessage(ev);
                        resolve(true);
                    }
                }
            }
            socket.onerror = (ev) => {
                console.error(ev);
                this.isConnected = false;
                if (this.onClose)
                    this.onClose();
                resolve(false);
            }
            socket.onclose = (ev) => {
                console.error(ev);
                this.isConnected = false;
                if (this.onClose)
                    this.onClose(ev.reason);
                resolve(false);
            }
        });
    }

    public dispatchEvent(name: string, data: any): Promise<void> {
        const frame = {
            op: OpCodes.Dispatch,
            d: {
                event: name,
                payload: data
            }
        }

        return this.sendFrame(frame);
    }

    public addListener(event: string, listener: (data: any) => void) {
        this.listeners.push({event, listener});
    }

    public removeListener(event: string, listener: (data: any) => void) {
        const index = this.listeners.indexOf({event, listener})
        if (index > -1) {
            this.listeners.splice(index, 1);
        }
    }

    private async handleMessage(ev: MessageEvent) {
        var msg = JSON.parse(ev.data) as SocketFrame;

        switch (msg.op) {
            case OpCodes.Dispatch:
                this.handleDispatch(msg);
                break;
            case OpCodes.Heartbeat:
                await this.sendFrame({
                    op: OpCodes.HeartbeatAck,
                    d: null
                })
                console.log("heartbeat ack")
                break;
        }
    }

    private handleDispatch(frame: SocketFrame) {
        const {event, payload} = frame.d;

        for (const listener of this.listeners) {
            if (listener.event === event) {
                listener.listener(payload);
            }
        }
    }

    private async sendHandshake(auth: string, events: string[], page: string) {
        const frame = {
            op: OpCodes.Handshake,
            d: {
                auth,
                events,
                page
            }
        }

        await this.sendFrame(frame);
    }

    private async sendFrame(frame: SocketFrame) {
        const json = JSON.stringify(frame);

        if (this.socket.readyState === this.socket.OPEN) {
            this.socket.send(json);
        }
    }
}
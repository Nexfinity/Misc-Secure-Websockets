import { OpCodes } from "./OpCodes";

export interface SocketFrame {
    op: OpCodes;
    d: any;
}
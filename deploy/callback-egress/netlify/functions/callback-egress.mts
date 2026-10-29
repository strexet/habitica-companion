import {handleGateway} from '../../src/gateway.mjs';
export default async (request: Request) => handleGateway(request, process.env);
export const config = {path:['/callback-egress','/callback-egress/health']};

const fs = require('node:fs');
const path = require('node:path');

const directOrdersUrl = process.env.FRONTEND_DIRECT_ORDERS_URL ?? 'http://localhost:5000/api/orders';
const awsGatewayOrdersUrl = process.env.FRONTEND_AWS_API_GATEWAY_ORDERS_URL ?? directOrdersUrl;
const outputPath = path.join(__dirname, '..', 'src', 'environments', 'environment.ts');
const content = `export const environment = {
  directOrdersUrl: ${JSON.stringify(directOrdersUrl)},
  awsGatewayOrdersUrl: ${JSON.stringify(awsGatewayOrdersUrl)}
};
`;

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, content);

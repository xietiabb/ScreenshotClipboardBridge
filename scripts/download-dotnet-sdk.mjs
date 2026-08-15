/**
 * 下载 .NET 8 SDK（win-x64 zip）到本地目录。
 *
 * 为什么存在：部分机器 schannel 损坏（AcquireCredentialsHandle 报 SEC_E_NO_CREDENTIALS），
 * 导致 PowerShell 的 Invoke-WebRequest / curl / .NET HttpClient 全部无法走 HTTPS。
 * Node.js 使用 OpenSSL 实现 TLS，不受影响，因此用它完成下载引导。
 *
 * 用法：node scripts/download-dotnet-sdk.mjs
 * 输出：环境变量 SDK_INSTALL_DIR（默认 %LOCALAPPDATA%\Microsoft\dotnet8）下解压后的 SDK；
 *       并打印 SDK 版本与 zip 路径。
 */
import fs from 'node:fs';
import path from 'node:path';
import { Readable } from 'node:stream';

const metaUrl = 'https://dotnetcli.azureedge.net/dotnet/release-metadata/8.0/releases.json';
const installDir = process.env.SDK_INSTALL_DIR
  || path.join(process.env.LOCALAPPDATA || '.', 'Microsoft', 'dotnet8');

console.log('Fetching release metadata...');
const meta = await (await fetch(metaUrl)).json();
const latestSdk = meta['latest-sdk'];
const release = meta.releases.find((r) => r['sdk']?.version === latestSdk);
if (!release) throw new Error(`SDK release not found: ${latestSdk}`);

const file = release['sdk'].files.find((f) => f.rid === 'win-x64' && f.name.endsWith('.zip'));
if (!file) throw new Error('win-x64 zip entry not found in metadata');

const zipPath = path.join(process.env.TEMP, `dotnet-sdk-${latestSdk}-win-x64.zip`);
console.log(`SDK version : ${latestSdk}`);
console.log(`Download URL: ${file.url}`);
console.log(`Zip path    : ${zipPath}`);

const res = await fetch(file.url);
if (!res.ok) throw new Error(`download failed: HTTP ${res.status}`);
const out = fs.createWriteStream(zipPath);
await new Promise((resolve, reject) => {
  const body = Readable.fromWeb(res.body);
  body.pipe(out);
  body.on('error', reject);
  out.on('finish', resolve);
  out.on('error', reject);
});
console.log('Download complete, bytes =', fs.statSync(zipPath).size);
console.log('ZIP=' + zipPath);
console.log('INSTALL_DIR=' + installDir);

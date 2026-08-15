/**
 * 离线 NuGet 包引导下载器。
 *
 * 为什么存在：本机 schannel 损坏，dotnet 的 HTTPS 无法连通 nuget.org。
 * Node.js 走 OpenSSL TLS，可正常访问，因此用它把「构建所需的全部 NuGet 包」
 * 解析（含传递依赖）并下载到本地目录 tools/nuget-local，
 * 之后 dotnet restore 通过 nuget.config 的本地源 + 本地全局包目录完成离线构建。
 *
 * 用法：node scripts/fetch-nuget.mjs
 * 覆盖根包列表：$env:NUGET_PACKAGES = '[["xunit","2.9.2"],...]' 后运行。
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// 以脚本自身位置为锚点（脚本位于 <repo>/scripts/，仓库根 = 上一级）
const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUT_DIR = path.join(REPO_ROOT, 'tools', 'nuget-local');
const TARGET_TFM = 'net8.0';

/** 根包（id, 精确版本）。Windows SDK 投影版本 10.0.19041.56 对应 SDK 8.0.4xx。 */
const DEFAULT_ROOTS = [
  ['Microsoft.Windows.SDK.NET.Ref', '10.0.19041.56'],
  ['Microsoft.NET.Test.Sdk', '17.12.0'],
  ['xunit', '2.9.2'],
  ['xunit.runner.visualstudio', '2.8.2'],
];

/** 把 TFM 字符串换算成可比较的等级（数值越大越"新"；net8.0 基线 = 800）。 */
function rankTfm(tfm) {
  if (!tfm) return -1;
  const t = tfm.toLowerCase().replace(/\./g, '');
  if (t.startsWith('net8')) return 800;
  if (t.startsWith('net7')) return 700;
  if (t.startsWith('net6')) return 600;
  if (t.startsWith('net5')) return 500;
  if (t.startsWith('netcoreapp3.1'.replace('.', ''))) return 431;
  if (t.startsWith('netcoreapp3.0'.replace('.', ''))) return 430;
  if (t.startsWith('netcoreapp2.2'.replace('.', ''))) return 422;
  if (t.startsWith('netcoreapp2.1'.replace('.', ''))) return 421;
  if (t.startsWith('netcoreapp2.0'.replace('.', ''))) return 420;
  if (t === 'netstandard21') return 411;
  if (t === 'netstandard20') return 410;
  if (t.startsWith('netstandard1')) return 300 + Number(t.replace('netstandard', ''));
  if (t.startsWith('net48')) return 280;
  if (t.startsWith('net472')) return 272;
  if (t.startsWith('net47')) return 270;
  if (t.startsWith('net462')) return 262;
  if (t.startsWith('net461')) return 261;
  if (t.startsWith('net46')) return 260;
  if (t.startsWith('net45')) return 250;
  if (t.startsWith('net4')) return 200;
  if (t.startsWith('portable')) return 50;
  if (t.startsWith('uap')) return 60;
  return 0;
}

/** 从 NuGet 版本范围字符串里取出最小版本（例如 "[1.2.3]" -> "1.2.3"，"1.2.3" -> "1.2.3"）。 */
function minVersion(range) {
  const m = String(range).match(/(\d+(?:\.\d+){0,3}(?:-[0-9A-Za-z.-]+)?)/);
  return m ? m[1] : null;
}

/** 抓取注册表（自动解压 gzip）。 */
async function getRegistration(id) {
  const url = `https://api.nuget.org/v3/registration5-gz-semver2/${id.toLowerCase()}/index.json`;
  const res = await fetch(url, { signal: AbortSignal.timeout(30000) });
  if (!res.ok) throw new Error(`registration ${id}: HTTP ${res.status}`);
  return res.json();
}

/** 在注册表中定位精确版本的 catalogEntry（处理分页）。 */
async function findCatalogEntry(id, version) {
  const reg = await getRegistration(id);
  const pages = Array.isArray(reg.items) ? reg.items : [];
  for (const page of pages) {
    if (Array.isArray(page.items)) {
      for (const item of page.items) {
        if (item.catalogEntry?.version === version) return item.catalogEntry;
      }
    } else if (page['@id']) {
      const leaf = await (await fetch(page['@id'])).json();
      for (const item of leaf.items ?? []) {
        if (item.catalogEntry?.version === version) return item.catalogEntry;
      }
    }
  }
  return null;
}

/** 为指定目标 TFM 挑选最合适的依赖组（就近原则）。 */
function pickGroup(groups, targetTfm) {
  const targetRank = rankTfm(targetTfm);
  let best = null;
  let bestRank = -Infinity;
  for (const g of groups ?? []) {
    const r = rankTfm(g.targetFramework);
    if (r > bestRank && r <= targetRank) {
      best = g;
      bestRank = r;
    }
  }
  if (!best) {
    // 无低于基线的组：兜底选等级最高的组
    for (const g of groups ?? []) {
      const r = rankTfm(g.targetFramework);
      if (r > bestRank) {
        best = g;
        bestRank = r;
      }
    }
  }
  return best;
}

async function main() {
  const roots = process.env.NUGET_PACKAGES ? JSON.parse(process.env.NUGET_PACKAGES) : DEFAULT_ROOTS;
  fs.mkdirSync(OUT_DIR, { recursive: true });

  const resolved = new Map(); // id(小写) -> 版本
  const queue = [...roots];
  const visited = new Set();

  while (queue.length > 0) {
    const [id, version] = queue.shift();
    const key = id.toLowerCase();
    if (visited.has(key)) continue;
    visited.add(key);

    const entry = await findCatalogEntry(id, version);
    if (!entry) throw new Error(`找不到包: ${id}@${version}`);
    resolved.set(key, version);

    const group = pickGroup(entry.dependencyGroups, TARGET_TFM);
    for (const dep of group?.dependencies ?? []) {
      const depVer = minVersion(dep.range);
      if (depVer) queue.push([dep.id, depVer]);
    }
  }

  console.log(`解析完成：共 ${resolved.size} 个包，开始下载...`);
  let downloaded = 0;
  for (const [id, version] of [...resolved.entries()].sort()) {
    const file = path.join(OUT_DIR, `${id}.${version}.nupkg`);
    if (fs.existsSync(file) && fs.statSync(file).size > 0) continue;
    const url = `https://api.nuget.org/v3-flatcontainer/${id}/${version}/${id}.${version}.nupkg`;
    const res = await fetch(url, { signal: AbortSignal.timeout(60000) });
    if (!res.ok) throw new Error(`下载失败 ${id}@${version}: HTTP ${res.status}`);
    fs.writeFileSync(file, Buffer.from(await res.arrayBuffer()));
    downloaded++;
    console.log(`  [${downloaded}] ${id} ${version}`);
  }

  const files = fs.readdirSync(OUT_DIR).filter((f) => f.endsWith('.nupkg'));
  const totalBytes = files.reduce((s, f) => s + fs.statSync(path.join(OUT_DIR, f)).size, 0);
  console.log(`完成：本地源 ${OUT_DIR} 共 ${files.length} 个包，${(totalBytes / 1024 / 1024).toFixed(1)} MB`);
}

main().catch((err) => {
  console.error('失败:', err.message);
  process.exit(1);
});

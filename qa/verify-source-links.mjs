import fs from 'node:fs';
import path from 'node:path';
const catalog=JSON.parse(fs.readFileSync('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json','utf8'));
const output=process.argv[2]||'qa/case-clarity/source-links.json';
const checks=await Promise.all(catalog.sources.map(async source=>{
  try {
    const response=await fetch(source.url,{signal:AbortSignal.timeout(30000)});
    const body=await response.text();
    const title=body.match(/<title[^>]*>([\s\S]*?)<\/title>/i)?.[1]?.trim()||response.headers.get('content-type');
    return {id:source.id,url:source.url,finalUrl:response.url,status:response.status,title,passed:response.ok&&!/page not found|404 not found/i.test(title||'')};
  } catch(error) {return {id:source.id,url:source.url,passed:false,error:error.message};}
}));
fs.mkdirSync(path.dirname(output),{recursive:true});
fs.writeFileSync(output,JSON.stringify({utc:new Date().toISOString(),passed:checks.every(c=>c.passed),checks},null,2)+'\n');
for(const c of checks)console.log(`${c.passed?'PASS':'FAIL'} ${c.id}: ${c.status||c.error} ${c.finalUrl||c.url}`);
process.exitCode=checks.every(c=>c.passed)?0:1;

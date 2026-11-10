// Read-only or deliberately unauthenticated probes. Never sends an email or deletes an account.
import {readFileSync,writeFileSync} from 'node:fs';
const config=JSON.parse(readFileSync(new URL('../../unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/supabase-config.json',import.meta.url),'utf8'));
const checks=[
  {name:'public auth configuration',path:'/auth/v1/settings',method:'GET',expected:200},
  {name:'anonymous progress sync denied',path:'/rest/v1/rpc/thorax_sync_progress',method:'POST',body:{expected_generation:0,orientation_completed:false,incoming_attempts:[],incoming_checkpoints:[]},expected:401},
  {name:'anonymous attempt read denied',path:'/rest/v1/thorax_attempts?select=id&limit=1',method:'GET',expected:401},
  {name:'account deletion without a session denied',path:'/functions/v1/thorax-delete-account',method:'POST',body:{},expected:401},
  {name:'account deletion rejects GET',path:'/functions/v1/thorax-delete-account',method:'GET',expected:405}
];
const results=await Promise.all(checks.map(async check=>{
  try {
    const response=await fetch(config.projectUrl+check.path,{method:check.method,headers:{apikey:config.publishableKey,'Content-Type':'application/json'},body:check.body?JSON.stringify(check.body):undefined,signal:AbortSignal.timeout(20000)});
    return {name:check.name,status:response.status,expected:check.expected,passed:response.status===check.expected};
  } catch(error) {
    return {name:check.name,status:null,expected:check.expected,passed:false,error:error.cause?.code??error.name};
  }
}));
const report={utc:new Date().toISOString(),project:config.projectUrl,checks:results,passed:results.every(r=>r.passed),emailsSent:0,authenticatedUserTests:false};
writeFileSync(new URL('../learning-live-api-report.json',import.meta.url),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));process.exitCode=report.passed?0:1;

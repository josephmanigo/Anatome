// Compact, verified equivalent of the additive migration for the dashboard editor.
import {PGlite} from '@electric-sql/pglite';
import {readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const root=new URL('../../',import.meta.url);
const read=p=>readFileSync(new URL(p,root),'utf8');
const catalog=JSON.parse(read('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json'));
const records=new Map(JSON.parse(read('unity/ThoraxLab/Assets/ThoraxLab/anatomy-catalog.json')).records.map(r=>[r.id,r]));
const units=catalog.units.filter(u=>!u.isCase).map(u=>({id:u.id,title:u.title,ids:u.anatomyIds,plan:u.variants.find(v=>v.mode===3).objectives.map(o=>[o.targetId,o.region,o.action])}));
const names=Object.fromEntries([...new Set(units.flatMap(u=>[...u.ids,...u.plan.map(p=>p[0])]))].map(id=>[id,[records.get(id).name,records.get(id).tissue==='bone']]));
const literal=value=>"'"+JSON.stringify(value).replaceAll("'","''")+"'::jsonb";
let sql=`BEGIN;
DO $migration$
DECLARE
 units jsonb := ${literal(units)};
 names jsonb := ${literal(names)};
 u jsonb; source_row record; step jsonb; plan jsonb; objectives jsonb; objective jsonb;
 target text; region text; label text; location text; oid text; previous_id text; instruction text; title text;
 opening boolean; bone boolean; ordered_steps boolean; mode integer; difficulty integer; action integer; i integer;
 generated jsonb; old_digest text; new_digest text;
BEGIN
 IF (SELECT count(*) FROM public.thorax_learning_definitions WHERE version='2026.10.1') <> 72 THEN
  RAISE EXCEPTION 'Expected 72 existing 2026.10.1 definitions';
 END IF;
 SELECT md5(string_agg(id||version||definition::text,'' ORDER BY version,id)) INTO old_digest
 FROM public.thorax_learning_definitions WHERE version <> '2026.10.2';
 FOR source_row IN SELECT * FROM public.thorax_learning_definitions WHERE version='2026.10.1' ORDER BY id LOOP
  generated := jsonb_set(source_row.definition,'{version}','"2026.10.2"'::jsonb);
  mode := (generated->>'mode')::integer;
  difficulty := (generated->>'difficulty')::integer;
  IF mode <> 0 THEN
   SELECT value INTO STRICT u FROM jsonb_array_elements(units) WHERE value->>'id'=generated->>'unitId';
   IF mode=3 THEN plan := u->'plan';
   ELSE
    SELECT jsonb_agg(jsonb_build_array(value,'all',CASE WHEN mode=1 THEN 0 ELSE 1 END) ORDER BY ordinality)
    INTO plan FROM jsonb_array_elements_text(u->'ids') WITH ORDINALITY
    WHERE ordinality <= CASE difficulty WHEN 0 THEN 3 WHEN 1 THEN 5 ELSE jsonb_array_length(u->'ids') END;
   END IF;
   ordered_steps := difficulty=0 OR mode=3;
   objectives := '[]'::jsonb; previous_id := NULL; i := 0;
   FOR step IN SELECT value FROM jsonb_array_elements(plan) LOOP
    target := step->>0; region := step->>1; action := (step->>2)::integer;
    label := names->target->>0; bone := (names->target->>1)::boolean; opening := action=2;
    location := CASE WHEN region='all' THEN '' ELSE ' on the specimen''s '||region||' side' END;
    oid := source_row.id||'-'||i||'-'||lower(target)||'-'||region;
    title := CASE WHEN opening THEN 'Remove a section of '||label||location WHEN action=1 THEN 'Place '||label ELSE 'Identify and preserve '||label END;
    instruction := CASE WHEN opening THEN
     CASE WHEN bone THEN 'Bone saw' ELSE 'Scalpel' END||' is equipped automatically. Point at '||label||location||', hold trigger and sweep a short, continuous cut. Release trigger, grip the separated chunk, lift it clear and release. Continue until this region''s removal meter is full. The remaining tissue stays anchored.'
     WHEN action=1 THEN 'Grip '||label||', match its ghost outline and release.'
     ELSE 'Point at '||label||' and press trigger. Examine its relationships and preserve it intact.' END;
    objective := jsonb_build_object(
     'id',oid,'title',title,'instruction',instruction,
     'explanation',CASE WHEN opening THEN 'A tissue section from '||label||location||' was cut and lifted. The next prepared station does not imply that all remaining tissue was removed.' ELSE label||' examined.' END,
     'targetId',target,'action',action,'region',region,
     'narration','anatomy-'||(u->>'id')||'-'||mode||'-'||i||'-'||region,
     'toolId',CASE WHEN opening THEN CASE WHEN bone THEN 'dissection-saw' ELSE 'dissection-scalpel' END ELSE '' END,
     'equipmentState',CASE WHEN opening THEN 'chunk-lifted:'||region ELSE '' END,
     'essential',true,'weight',1,
     'maxDistance',CASE WHEN mode=2 THEN (ARRAY[.12,.065,.04])[difficulty+1] ELSE (ARRAY[.075,.04,.02])[difficulty+1] END,
     'maxAngle',CASE WHEN mode=2 THEN (ARRAY[55,35,20])[difficulty+1] ELSE (ARRAY[35,20,10])[difficulty+1] END,
     'minContactSeconds',0,'minPathFraction',CASE WHEN opening THEN (ARRAY[.003,.005,.008])[difficulty+1] ELSE 0 END,
     'prerequisites',CASE WHEN ordered_steps AND i>0 THEN jsonb_build_array(previous_id) ELSE '[]'::jsonb END);
    objectives := objectives||jsonb_build_array(objective); previous_id := oid; i := i+1;
   END LOOP;
   generated := generated||jsonb_build_object('ordered',ordered_steps,'objectives',objectives,
    'debrief','Review missed structures and repeat the activity. Prepared views and chunk extraction are teaching aids, not clinical procedures.');
   IF mode=3 THEN generated := generated||jsonb_build_object('briefing',(u->>'title')||': complete every anatomical station. '||
    (ARRAY['Highlighted targets and coached steps.','Fewer cues and a larger required opening.','Independent work with the strictest removal requirement.'])[difficulty+1]||
    ' Exterior coverings are prepared for this topic; only your own cut-and-lift actions earn credit.'); END IF;
  END IF;
  INSERT INTO public.thorax_learning_definitions(id,version,definition) VALUES(source_row.id,'2026.10.2',generated)
  ON CONFLICT(id,version) DO UPDATE SET definition=excluded.definition;
 END LOOP;
 SELECT md5(string_agg(id||version||definition::text,'' ORDER BY version,id)) INTO new_digest
 FROM public.thorax_learning_definitions WHERE version <> '2026.10.2';
 IF new_digest IS DISTINCT FROM old_digest THEN RAISE EXCEPTION 'Historical definitions changed'; END IF;
 -- EXPECTED_DIGEST_CHECK
END $migration$;
COMMIT;
SELECT version,count(*) AS definitions,md5(string_agg(id||definition::text,'' ORDER BY id)) AS definition_digest
FROM public.thorax_learning_definitions GROUP BY version ORDER BY version;
`;
const db=new PGlite();
await db.exec('create table public.thorax_learning_definitions(id text,version text,definition jsonb,primary key(id,version));');
await db.exec(read('supabase/migrations/202610040001_clinical_reasoning_content.sql'));
await db.exec(sql);
const rows=(await db.query("select id,definition from public.thorax_learning_definitions where version='2026.10.2' order by id")).rows;
const variants=catalog.units.flatMap(u=>u.variants);
assert.equal(rows.length,72);
for(const row of rows)assert.deepEqual(row.definition,variants.find(v=>v.id===row.id),row.id);
const digest=(await db.query("select md5(string_agg(id||definition::text,'' order by id)) as digest from public.thorax_learning_definitions where version='2026.10.2'")).rows[0].digest;
sql=sql.replace('-- EXPECTED_DIGEST_CHECK',`IF (SELECT count(*) FROM public.thorax_learning_definitions WHERE version='2026.10.2') <> 72 OR
 (SELECT md5(string_agg(id||definition::text,'' ORDER BY id)) FROM public.thorax_learning_definitions WHERE version='2026.10.2') <> '${digest}'
 THEN RAISE EXCEPTION 'Deployed definitions do not match the tested APK curriculum'; END IF;`);
await db.exec(sql);
writeFileSync(new URL('.tmp/dissection-dashboard.sql',root),sql);
writeFileSync(new URL('qa/dissection-overhaul/deployment-equivalence.json',root),JSON.stringify({utc:new Date().toISOString(),passed:true,exactDefinitions:rows.length,digest,bytes:Buffer.byteLength(sql),liveSupabase:false},null,2));
console.log(JSON.stringify({passed:true,exactDefinitions:rows.length,digest,bytes:Buffer.byteLength(sql)}));
await db.close();

// Prepared anatomical stations share the full topic at every difficulty.
// Cutting is an educational volume extraction, not a clinical incision plan.
export function dissectionPlan(id, anatomyIds) {
 const cut=(targetId,region='all')=>({targetId,region,action:2});
 const inspect=targetId=>({targetId,region:'all',action:0});
 const bilateral=targetId=>[cut(targetId,'left'),cut(targetId,'right')];
 const openings={
  'chest-wall': [...['FMA7163','REC_fat','REC_fascia'].flatMap(bilateral),
   ...['FMA79979','FMA79980','FMA13375','FMA13376'].map(x=>cut(x)),
   ...['FMA9756','FMA9757','FMA9758'].flatMap(bilateral),
   ...['FMA7957','FMA8148','FMA8066','FMA8093'].map(x=>cut(x))],
  'pleura-lungs': ['REC_Right_Parietal_Pleura','REC_Right_Visceral_Pleura','MAL_RUL','MAL_RML','MAL_RLL',
   'REC_Left_Parietal_Pleura','REC_Left_Visceral_Pleura','MAL_LUL','MAL_LLL'].map(x=>cut(x)),
  'airways': ['FMA7394','FMA68418','FMA7396'].map(x=>cut(x)),
  'heart-coronaries': [cut('REC_Pericardium'),inspect('FMA9457'),inspect('FMA9531'),inspect('FMA13884')],
  'vessels-valves': ['FMA3736','FMA8612'].map(x=>cut(x)),
  'mediastinum': [cut('FMA7131')]
 };
 const steps=openings[id];if(!steps)throw Error('No dissection station: '+id);
 return [...steps,...anatomyIds.filter(x=>!steps.some(o=>o.targetId===x)).map(inspect)];
}

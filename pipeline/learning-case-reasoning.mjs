// Original teaching decisions. Fixed case indications remain separate from real clinical prescribing.
const mechanisms=[
 ['pneumothorax','Pleural air','Air in the pleural space separates the lung from the chest wall.'],
 ['pleural-effusion','Pleural fluid','Fluid outside the lung compresses its surrounding pleural compartment.'],
 ['asthma','Narrowed airway','Airway narrowing restricts airflow; it is not a pleural collection.'],
 ['pneumonia','Inflamed air spaces','Inflammatory material occupies affected alveolar air spaces.'],
 ['coronary-disease','Narrowed coronary lumen','Coronary plaque limits the vessel lumen and myocardial blood supply.'],
 ['aortic-stenosis','Restricted valve opening','Restricted aortic cusps obstruct forward flow from the left ventricle.']
];
const responseLabels={pneumothorax:'Lung re-expands','pleural-effusion':'Less pleural fluid',asthma:'Airway widens',pneumonia:'Gradual clearing','coronary-disease':'Wider treated lumen','aortic-stenosis':'Replacement opens'};
function decision(title,instruction,id,choices,correct,explanation,criticalOnWrong=false){
 return {title,instruction,targetId:'equipment:'+id,action:10,toolId:correct,equipmentState:'placed',choices,explanation,criticalOnWrong};
}
export function reasoningSteps(caseId,list){
 const index=mechanisms.findIndex(m=>m[0]===caseId);if(index<0)throw Error('Unknown reasoning case '+caseId);
 const mechanismChoices=[mechanisms[index],mechanisms[(index+1)%6],mechanisms[(index+3)%6]].map(([model,label,rationale])=>({id:'mechanism:'+model,label,model,response:0,rationale:'Compare this cutaway with the observed anatomy. '+rationale}));
 const mechanism=decision('Explain the anatomical mechanism','Inspect the specimen and three cutaways. Grip the mechanism that explains this case and release it into the patient-plan dock.','reason-mechanism',mechanismChoices,'mechanism:'+caseId,mechanisms[index][2]);
 const responseChoices=[
  {id:'response:improved',label:responseLabels[caseId],model:caseId,response:1,rationale:'The represented treatment addresses this anatomical mechanism.'},
  {id:'response:unchanged',label:'Unchanged anatomy',model:caseId,response:0,rationale:'An unchanged model does not show the intended structural response. Reassess the mechanism and treatment.'},
  {id:'response:other',label:mechanisms[(index+2)%6][1],model:mechanisms[(index+2)%6][0],response:1,rationale:'This is a different compartment or mechanism. Match the response to the anatomy actually treated.'}
 ];
 const response=decision('Predict the structural response','Before reassessing the patient, place the cutaway showing the expected anatomical response in the dock. Model changes are illustrative; improvement does not prove clinical recovery.','reason-response',responseChoices,'response:improved',caseId==='pneumonia'?'Inflammatory air-space changes improve over time; oxygen support alone does not eliminate infection.':'The model shows the intended anatomical change. Clinical benefit and complications still require reassessment.');
 const result=[list[0],mechanism,...list.slice(1,-1),response,list.at(-1)];
 if(caseId==='pleural-effusion'){
  const safety=decision('New chest discomfort during aspiration','The patient develops chest discomfort while fluid is being aspirated. Place the next-action symbol into the dock.','reason-safety',[
   {id:'pause-review',label:'Stop and reassess',rationale:'Stop aspiration and assess the new symptom before considering further treatment.'},
   {id:'continue',label:'Continue draining',rationale:'Continuing despite new chest discomfort is unsafe in this authored case. Stop aspiration and reassess.'},
   {id:'dismiss',label:'Dismiss the symptom',rationale:'A new symptom during aspiration requires attention. Stop aspiration and reassess.'}
  ],'pause-review','Aspiration stops when chest discomfort develops. The next step records the symptom assessment.',true);
  result.splice(result.findIndex(o=>o.title==='Stop and check new chest discomfort'),0,safety);
 }
 return result;
}

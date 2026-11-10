// Original, concise teaching scenarios mapped to the primary sources in the catalog.
// Every action is an interaction in the scene; no button directly completes a procedure.
const step=(title,instruction,targetId,action,toolId='',equipmentState='',explanation='')=>({title,instruction,targetId,action,toolId,equipmentState,explanation:explanation||'Step complete. Follow the next single instruction.'});
const inspect=(title,text,id)=>step(title,text,id,3);
const contact=(title,text,id,tool='probe',state='ready')=>step(title,text,`equipment:${id}`,5,tool,state);
const activate=(title,text,id,tool,state='ready')=>step(title,text,`equipment:${id}`,6,tool,state);
const trace=(title,text,id,tool,state='ready')=>step(title,text,`equipment:${id}`,7,tool,state);
const connect=(title,text,id,tool='connector')=>step(title,text,`equipment:${id}`,4,tool,'connected');
const reassess=(title,text,id)=>step(title,text,id,8,'probe','ready');
const safety=(detail)=>contact('Check the patient and plan',`${detail} Touch the chart with the assessment pointer. Check identity, consent, allergies and the agreed treatment.`,'chart');

export const casePathways=[
 {
  id:'pneumothorax',title:'Pneumothorax',summary:'A breathless adult has a right pneumothorax. The case team has selected a small-bore chest drain.',
  lesson:'Air separates the right lung from its chest wall. Review the chest X-ray and the agreed drainage site. Work through a small-bore, wire-guided drain sequence on the enlarged chest-wall model. Its skin, rib, intercostal bundle, pleural space and lung show what each instrument must preserve.',
  anatomyIds:['MAL_RUL','MAL_RML','MAL_RLL','REC_Right_Parietal_Pleura','FMA8066'],protectedIds:['REC_Right_IC4_Nerve','REC_Right_IC4_Artery','FMA13295','MAL_RLL'],pathway:'Small-bore chest drain',
  response:'Air drains through the connected water seal. Recheck breathing, drain function and chest imaging; watch for pain, bleeding, infection or renewed breathlessness.',sourceIds:['bts','bts-detail','bts-safety','pleural'],
  objectives:[
   inspect('Find the collapsed lung','Point to the right superior lobe. Compare its size with the opposite lung.','MAL_RUL'),
   safety('The chest image and indication are already established.'),
   contact('Check the planned intercostal space','Locate the upper edge of the lower rib on the cross-section. Avoid the bundle beneath the rib above.','pleural-site'),
   activate('Prepare a sterile field','Move the swab onto the marked skin window. Hold the trigger to clean it; the sterile drape opens.','pleural-site','swab'),
   activate('Represent local anaesthesia','Place the anaesthetic syringe at the skin target. The case supplies the checked prescription.','pleural-site','anaesthetic'),
   contact('Confirm pleural access','Guide the aspiration needle to the pleural-space endpoint. Air appears in the syringe before the wire step opens.','pleural-end','needle'),
   trace('Advance the guidewire','Move the wire through each marker into the air space. Preserve the lung beyond the endpoint.','pleural-end','guidewire'),
   trace('Make the skin opening','Move the scalpel along the short skin line beside the retained wire.','pleural-incision','scalpel'),
   trace('Open the drain tract','Advance the short dilator along the wire route. Stop at the endpoint; do not push toward the lung.','pleural-end','dilator'),
   trace('Place the drain over the wire','Follow the same route with the drain. The training model then withdraws the wire and retains the drain.','pleural-end','drain'),
   connect('Connect the water seal','Fit the connector into the drain-bottle port. Keep the bottle below the chest.','drain-circuit'),
   trace('Fix the holding suture','Follow the fixation loop around the drain with the needle holder. The holding suture secures the tube.','pleural-fixation','suture','connected'),
   activate('Secure the drain','Place the fixation dressing over the entry site. Confirm the tube is secured.','pleural-site','dressing','connected'),
   reassess('Check the response','Touch the affected lobe with the assessment pointer. Inspect expansion and the connected drain.','MAL_RUL')
  ]
 },
 {
  id:'asthma',title:'Asthma',summary:'An adult with airway narrowing has a prescribed metered-dose inhaler and spacer. Help the virtual patient use it.',
  lesson:'Follow the narrowing from the bronchus into the enlarged airway. The spacer holds a single puff while the virtual patient breathes it in. Learn device preparation, a mouth seal, one actuation and a slow inhalation. This sequence is for the represented metered-dose inhaler, not every inhaler type.',
  anatomyIds:['FMA7394','FMA68418','FMA7396','MAL_RUL'],protectedIds:['FMA7131'],pathway:'Metered-dose inhaler and spacer',response:'The airway opens as prescribed reliever treatment takes effect. Reassess breathing and technique; the patient still needs an asthma action plan.',sourceIds:['asthma','spacer'],
  objectives:[
   inspect('Find the narrowed airway','Select the right main bronchus. Look into the enlarged airway beside the specimen.','FMA68418'),
   safety('Confirm the prescribed device and medicine.'),
   trace('Remove the cap','Grip the cap and pull it away along the short arrow. Inspect the open mouthpiece.','inhaler-cap','cap'),
   trace('Shake the inhaler','Grip the inhaler. Move it through the three shake markers.','inhaler-prep','inhaler'),
   connect('Fit the inhaler to the spacer','Insert the inhaler mouthpiece into the rear of the spacer.','spacer','inhaler'),
   contact('Seal the mouthpiece','Let the virtual patient breathe out away from the spacer, then hold the mouthpiece against the lips.','mouthpiece','spacer','connected'),
   activate('Release one puff','Press once with the inhaler aligned at the spacer. One dose enters the chamber.','spacer','inhaler','connected'),
   contact('Guide a slow breath in','Keep the mouthpiece sealed during the green inspiration phase. The virtual patient then pauses comfortably before breathing out.','mouthpiece','spacer','inspiration'),
   reassess('Check breathing and technique','Touch the bronchus with the assessment pointer. Compare the airway opening and review the action plan.','FMA68418')
  ]
 },
 {
  id:'coronary-disease',title:'Coronary artery disease',summary:'A coronary lesion has been selected for PCI after assessment. Follow a radial-access balloon and stent case.',
  lesson:'Coronary treatment begins with a plan, imaging and access through an artery. Follow the enlarged route from wrist to aorta and then into the selected coronary artery. A wire crosses the narrowing before a balloon or stent follows it. The stent remains after the delivery devices are removed.',
  anatomyIds:['FMA50040','FMA3736','FMA50039','FMA4706'],protectedIds:['FMA7252','FMA7253','FMA7254'],pathway:'Radial-access PCI',response:'Final vessel imaging shows the treated lumen. Check the wrist for bleeding and continue the prescribed coronary and antiplatelet care; stenting does not cure the underlying disease.',sourceIds:['coronary','pci-process','pci-aftercare','arterial-routes'],
  objectives:[
   inspect('Find the affected coronary artery','Select the left coronary artery. Inspect the plaque in the enlarged cutaway.','FMA50040'),
   safety('Review the angiogram, selected lesion and contrast history.'),
   activate('Prepare radial access','Use the swab on the wrist target of the enlarged vascular model. Local anaesthesia and access checks are represented.','radial-access','swab'),
   connect('Connect the guiding catheter','Seat the catheter connector in the access sheath port.','catheter-system'),
   trace('Follow the arterial route','Follow right radial, brachial, axillary, subclavian, brachiocephalic and aortic markers to the coronary origin.','coronary-origin','guidewire','connected'),
   trace('Cross the selected lesion','Follow the coronary channel through the narrowing. Stop at the wire endpoint.','coronary-lesion','guidewire','connected'),
   contact('Place the balloon','Align the balloon within the lesion over the represented wire.','coronary-lesion','balloon','connected'),
   activate('Dilate the selected lesion','Activate the balloon. Watch it expand, then deflate before advancing.','coronary-lesion','balloon','connected'),
   activate('Deploy the stent','Position the stent at the same lesion and activate. The scaffold remains in the artery.','coronary-lesion','stent','connected'),
   trace('Withdraw the delivery device','Move the catheter back along the withdrawal markers. The stent stays in place.','catheter-withdrawal','balloon','connected'),
   activate('Close the access site','Fit the compression band around the radial access model.','radial-access','dressing','connected'),
   reassess('Review final flow','Probe the left coronary artery. Compare the lumen and review the follow-up plan.','FMA50040')
  ]
 },
 {
  id:'pneumonia',title:'Pneumonia',summary:'An adult has confirmed bacterial pneumonia, prescribed antibiotics and an oxygen order for hypoxaemia.',
  lesson:'Inflammatory material fills some air spaces in the affected lower lobe. Review the patient and the prescription, give the ordered care, then reassess. Respiratory support improves oxygen delivery; it does not remove infection. Antibiotics and recovery are represented on a clearly compressed follow-up timeline.',
  anatomyIds:['MAL_RLL','FMA31747','FMA13295','FMA50872'],protectedIds:['REC_Right_Phrenic_nerve'],pathway:'Prescribed pneumonia care',response:'Review response and microbiology. Review intravenous treatment by 48 hours. Fever often settles before cough and fatigue; persistent or worsening symptoms need reassessment.',sourceIds:['pneumonia','pneumonia-care'],
  objectives:[
   inspect('Find the affected lobe','Select the right inferior lobe. Compare air spaces with the clear neighboring tissue.','MAL_RLL'),
   safety('Review severity, allergies and the written antibiotic and oxygen orders.'),
   contact('Record a respiratory baseline','Place the monitoring probe on the virtual patient\'s finger. Review the qualitative breathing and oxygenation indicators.','monitor'),
   contact('Check the medication order','Touch the prescription dock with the labeled medicine. Match patient, medicine, route and the supplied order.','prescribed-care','medication'),
   connect('Prepare the prescribed route','Join the prepared administration set to its labeled port.','prescribed-care'),
   activate('Administer the ordered treatment','Seat the checked medicine in its administration port. Hold the trigger to represent the prescribed administration.','prescribed-care','medication','connected'),
   activate('Apply the oxygen order','Fit the support mask to the virtual patient. The case uses its supplied oxygen order.','mouthpiece','oxygen','connected'),
   contact('Recheck respiratory support','Return the monitoring probe to the finger. Compare the response with baseline.','monitor'),
   reassess('Review the follow-up timeline','Probe the affected lobe. Watch the days-and-weeks timeline; recovery is gradual.','MAL_RLL')
  ]
 },
 {
  id:'pleural-effusion',title:'Pleural effusion',summary:'A symptomatic right pleural collection has been selected for ultrasound-guided therapeutic aspiration.',
  lesson:'The fluid lies outside the lung. Ultrasound helps locate the fluid, diaphragm and lung before access. Follow preparation, a short catheter route, controlled aspiration and monitoring. The enlarged cross-section keeps the needle away from the rib bundle and the deeper lung.',
  anatomyIds:['REC_Right_Parietal_Pleura','MAL_RLL','FMA13295','FMA8175'],protectedIds:['FMA13295','REC_Right_IC6_Nerve','REC_Right_IC6_Artery','MAL_RLL'],pathway:'Ultrasound-guided aspiration',response:'Fluid removal relieves compression in this case. Monitor symptoms and the puncture site; send the planned sample and investigate why fluid accumulated.',sourceIds:['bts','bts-detail','bts-safety','pleural'],
  objectives:[
   inspect('Find the pleural collection','Select the right parietal pleura. Identify fluid outside the lower lung.','REC_Right_Parietal_Pleura'),
   safety('Review the aspiration indication and bleeding-risk assessment.'),
   contact('Locate fluid with ultrasound','Hold the ultrasound probe at the side-window. Identify fluid above the diaphragm and outside the lung.','pleural-site','ultrasound'),
   activate('Prepare the skin','Use the swab over the ultrasound-selected window.','pleural-site','swab'),
   activate('Represent local anaesthesia','Place the checked anaesthetic syringe at the entry target.','pleural-site','anaesthetic'),
   connect('Prepare controlled aspiration','Connect the syringe and collection circuit. This case uses controlled aspiration without wall suction.','aspiration'),
   trace('Advance the aspiration catheter','Follow the short channel above the lower rib. Stop in the fluid; preserve deeper lung and diaphragm.','pleural-end','aspirator','connected'),
   activate('Begin slow aspiration','Hold the aspiration control and watch the fluid fall. A symptom cue will ask you to pause.','pleural-end','aspirator','connected'),
   contact('Stop and check new chest discomfort','Release aspiration. Touch the monitoring station with the probe to record the symptom check.','monitor'),
   trace('Withdraw and dress the site','Withdraw the aspiration catheter along the return markers. The dressing is applied after removal.','pleural-withdrawal','aspirator','connected'),
   reassess('Review symptoms and the cause','Probe the right lower lobe. Compare expansion and review the fluid investigation plan.','MAL_RLL')
  ]
 },
 {
  id:'aortic-stenosis',title:'Aortic valve stenosis',summary:'A heart team has selected transfemoral TAVI for severe aortic stenosis in a fictional adult.',
  lesson:'The replacement travels through a femoral artery and the aorta to the native valve. It expands inside that valve rather than removing it. Follow the enlarged vessel model, position the replacement, then check function, coronary openings and the access site.',
  anatomyIds:['FMA7253','FMA7252','FMA7254','FMA3736','FMA50040'],protectedIds:['FMA50040','FMA50039'],pathway:'Transfemoral TAVI',response:'The replacement opens within the native valve. Continue rhythm, valve and groin-site monitoring; watch for leakage, bleeding, stroke symptoms and conduction problems.',sourceIds:['valves','tavr','tavi-process','tavi-assessment','arterial-routes'],
  objectives:[
   inspect('Find the restricted valve','Select the anterior aortic cusp. Watch the reduced opening in the cutaway.','FMA7253'),
   safety('Review the heart-team decision and the planned CT-sized device.'),
   activate('Prepare femoral access','Use the swab at the groin target. Local anaesthesia and access preparation are represented.','femoral-access','swab'),
   connect('Prepare the delivery system','Connect the device handle to the femoral sheath port.','valve-system'),
   trace('Follow the aorta to the valve','Follow femoral, iliac, abdominal aorta, descending aorta, arch and ascending aorta markers to the valve.','aortic-valve','guidewire','connected'),
   contact('Align the replacement valve','Place the delivery tip across the native valve. Keep the coronary openings unobstructed.','aortic-valve','valve','connected'),
   activate('Deploy inside the native valve','Activate the selected self-expanding replacement. The old cusps remain around its scaffold.','aortic-valve','valve','connected'),
   contact('Check position and forward flow','Use the assessment pointer at the implanted valve. Review opening, leakage and the coronary relationship.','aortic-valve'),
   trace('Withdraw the delivery system','Return the delivery handle along the withdrawal path, leaving the replacement in place.','valve-withdrawal','valve','connected'),
   activate('Close and inspect femoral access','Place the closure dressing at the groin site and inspect the access model.','femoral-access','dressing','connected'),
   reassess('Review valve and rhythm follow-up','Probe the aortic cusp. Check the represented forward flow and post-procedure monitoring plan.','FMA7253')
  ]
 }
];

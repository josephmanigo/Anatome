using System;
using System.Linq;
using ThoraxLab.Learning;

static class CaseIncisionTests
{
    public static void Run(LearningCatalog catalog, Action<bool,string> check)
    {
        const float length = .024f;
        var stroke = new CaseIncisionStroke(length);
        bool Sample(float fraction,float across=0,float depth=0,bool aligned=true,bool edge=true,float dt=1f/72)
            => stroke.Sample(fraction*length,across,depth,aligned,edge,dt);
        var cases=catalog.units.Where(u=>u.isCase).SelectMany(u=>u.variants).ToArray();
        check(cases.All(v=>v.objectives.All(o=>o.toolId!="dissection-saw")),"A case offered a dissection saw without a bone objective.");
        var cuts=cases.SelectMany(v=>v.objectives).Where(o=>o.toolId=="scalpel").ToArray();
        check(cuts.Length==3&&cuts.All(o=>o.action==ActionKind.Trace&&o.targetId=="equipment:pleural-incision"),"The three authored incisions must share the surface check.");
        for(int i=0;i<=12;i++){Sample(i/12f,depth:-.006f);Sample(i/12f,depth:.006f);Sample(i/12f,across:.006f);}
        check(stroke.Progress==0&&stroke.ContactSeconds==0&&!stroke.Touching,"Hovering, off-axis contact or deep penetration marked the insert.");
        for(int i=0;i<=12;i++){Sample(i/12f,aligned:false);Sample(i/12f,edge:false);}
        check(stroke.Progress==0&&!stroke.Touching,"The spine or a transverse blade was counted as cutting.");
        Sample(0);Sample(1);
        check(!stroke.Touching&&stroke.Progress==0,"An endpoint jump crossed unobserved skin.");
        Sample(0);for(int i=0;i<120;i++)Sample(0);
        check(stroke.Touching&&stroke.Progress==0,"Holding still created an incision.");
        Sample(.25f);Sample(.50f);Sample(.35f);stroke.BreakContact();
        check(stroke.Progress==.5f&&!stroke.Touching,"Backing up or releasing moved the existing cut.");
        check(!Sample(.95f)&&!stroke.Touching&&!Sample(.05f)&&!stroke.Touching,"A released blade reacquired away from the cut frontier.");
        Sample(.5f);Sample(.75f);check(Sample(1)&&stroke.Progress==1,"The mark could not resume at its visible end.");
        stroke.Restore(true);
        check(stroke.Progress==1&&stroke.ContactSeconds==0&&!stroke.Touching,"A completed checkpoint did not restore as a released blade.");
        foreach(int hz in new[]{30,72,120})
        {
            stroke.Restore(false);
            check(stroke.Progress==0&&!stroke.Touching&&stroke.ContactSeconds==0,"Restart retained prior stroke state.");
            for(int i=0;i<=hz;i++)Sample((float)i/hz,dt:1f/hz);
            check(stroke.Progress==1&&Math.Abs(stroke.ContactSeconds-1)<.001f,"Equal motion produced different evidence at "+hz+" Hz.");
            foreach(var step in cuts){
                var evidence=new InteractionAction{kind=step.action,targetId=step.targetId,toolId=step.toolId,
                    equipmentState=step.equipmentState,contactVerified=true,pathFraction=stroke.Progress,contactSeconds=stroke.ContactSeconds};
                check(AssessmentEngine.Matches(step,evidence)==(stroke.ContactSeconds>=step.minContactSeconds),"The scenario ignored its contact time at "+hz+" Hz.");
                evidence.pathFraction=.5f;
                check(!AssessmentEngine.Matches(step,evidence),"An incomplete incision passed "+step.id);
            }
            for(int i=0;i<hz/2;i++)Sample(1,dt:1f/hz);
            check(cuts.All(step=>AssessmentEngine.Matches(step,new InteractionAction{kind=step.action,targetId=step.targetId,toolId=step.toolId,
                equipmentState=step.equipmentState,contactVerified=stroke.Touching,pathFraction=stroke.Progress,contactSeconds=stroke.ContactSeconds})),
                "Sufficient contact with a complete opening failed at "+hz+" Hz.");
        }
    }
}

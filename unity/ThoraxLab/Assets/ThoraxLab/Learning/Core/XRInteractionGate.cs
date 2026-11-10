using System;

namespace ThoraxLab.Learning
{
    public enum XRInteractionSource { None, Controller, BareHand, Simulator }

    // A held gesture/button must be released after tracking loss or a source
    // switch before it can cause a new selection, grab, cut or UI click.
    public sealed class XRInteractionGate
    {
        XRInteractionSource source;
        bool triggerArmed, gripArmed;
        public void Filter(XRInteractionSource next,bool tracked,ref bool trigger,ref bool grip)
        {
            if(!tracked||next==XRInteractionSource.None){source=XRInteractionSource.None;triggerArmed=gripArmed=false;trigger=grip=false;return;}
            if(source!=next){source=next;triggerArmed=gripArmed=false;}
            if(!trigger)triggerArmed=true;
            if(!grip)gripArmed=true;
            trigger&=triggerArmed;grip&=gripArmed;
        }
    }

    public sealed class HandGestureLatch
    {
        public bool Select { get; private set; }
        public bool Grab { get; private set; }
        public void Update(bool tracked,float indexPinch,float graspCurl)
        {
            if(!tracked||float.IsNaN(indexPinch)||float.IsNaN(graspCurl)||float.IsInfinity(indexPinch)||float.IsInfinity(graspCurl))
            {Select=Grab=false;return;}
            Select=Latch(Select,indexPinch);Grab=Latch(Grab,graspCurl);
        }
        static bool Latch(bool down,float value)=>value>=(down?.5f:.8f);
    }
}

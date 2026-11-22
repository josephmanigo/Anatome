using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Learning
{
    // THESIS: A floating glass instrument console, with equal controls and one clear learning action.
    // OWN-WORLD: Smoked blue glass, polished ice edges, ivory Source Sans 3, and sea-glass primary controls.
    // STORY: Learn trigger and grip, start Easy, follow one coached step, pass to unlock the next level.
    // FIRST VIEWPORT: A graspable brand rail sits above equal navigation keys; a scrollable lesson sits above reserved status space.
    // FORM: User-pinned 3D glass overrides seed 4eb97167. Immediate press tint; stationary bevels preserve VR comfort.
    // FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
    public static class LabWidgets
    {
        public static readonly Color Ground = new Color(.035f,.065f,.095f,1);
        public static readonly Color Surface = new Color(.105f,.17f,.215f,1);
        public static readonly Color Ink = new Color(.95f,.97f,.98f,1);
        public static readonly Color Muted = new Color(.72f,.82f,.87f,1);
        public static readonly Color Accent = new Color(.48f,.88f,.81f,1);
        public static float ControlHeight => Mathf.Ceil(48*TextScale+28);
        public static float BackHeight => Mathf.Max(58,Mathf.Ceil(36*TextScale+18));
        public static TMP_FontAsset Font;
        public static float TextScale = 1;
        public static bool HighContrast, AstigmatismReadingMode;
        public static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent,false); return rt;
        }
        public static void Fill(RectTransform rt, float inset=0) { rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.one*inset;rt.offsetMax=Vector2.one*-inset; }
        public static Image Panel(Transform parent,string name, Color color) { var rt=Rect(name,parent);var image=rt.gameObject.AddComponent<Image>();image.color=HighContrast&&color==Ground?Color.black:color;return image; }
        public static LayoutElement Size(Component item, float height=-1,float width=-1,float flexible=0)
        {
            var l=item.GetComponent<LayoutElement>()??item.gameObject.AddComponent<LayoutElement>();if(height>=0){l.minHeight=height;l.preferredHeight=height;}if(width>=0){l.minWidth=width;l.preferredWidth=width;}l.flexibleWidth=flexible;return l;
        }
        public static RectTransform Column(Transform parent,string name="Column",int padding=0,float spacing=12)
        {
            var rt=Rect(name,parent);var layout=rt.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(padding,padding,padding,padding);layout.spacing=spacing;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;return rt;
        }
        public static RectTransform Row(Transform parent,string name="Row",float spacing=10)
        {
            var rt=Rect(name,parent);var l=rt.gameObject.AddComponent<HorizontalLayoutGroup>();l.spacing=spacing;l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandHeight=true;l.childForceExpandWidth=true;return rt;
        }
        public static RectTransform NavigationRow(Transform parent,string name)
        {
            var row=Row(parent,name,12);var layout=row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth=layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.MiddleLeft;
            return row;
        }
        public static TextMeshProUGUI Text(Transform parent,string value,float size=22,Color? color=null)
        {
            var rt=Rect("Text · "+(value??""),parent);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();t.font=Font;t.text=value;t.fontSize=Mathf.Max(22,size)*TextScale;t.color=HighContrast&&(color==null||color==Muted)?Color.white:color??Ink;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;t.richText=false;t.lineSpacing=3;return t;
        }
        public static Button Button(Transform parent,string value,Action action,bool primary=false,bool enabled=true)
        {
            var image=LabUIVisuals.Surface(parent,value,primary?Accent:Surface);var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.interactable=enabled;
            var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.8f,1,1);colors.pressedColor=new Color(.62f,.8f,.78f);colors.selectedColor=new Color(.8f,1,1);colors.disabledColor=new Color(.43f,.48f,.50f);colors.fadeDuration=0;b.colors=colors;
            var l=image.gameObject.AddComponent<HorizontalLayoutGroup>();l.padding=new RectOffset(20,20,12,12);l.spacing=12;l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=false;l.childForceExpandHeight=false;l.childAlignment=TextAnchor.MiddleLeft;
            var t=Text(image.transform,value,24,primary?Ground:Ink);t.alignment=TextAlignmentOptions.MidlineLeft;var labelSize=Size(t,flexible:1);labelSize.minWidth=0;labelSize.preferredWidth=0;t.fontStyle=FontStyles.Bold;
            var size=Size(image,ControlHeight,flexible:1);size.minWidth=0;size.preferredWidth=0;size.preferredHeight=-1;
            image.gameObject.AddComponent<LabButtonHeight>();
            if(value.Length>5&&value!="Delete"&&!value.StartsWith("Shift "))LabUIVisuals.Icon(image.transform,LabUIVisuals.ButtonGlyph(value),primary?Ground:Muted,24);
            LabUIVisuals.Border(image.rectTransform,primary?new Color(.8f,1,1,.7f):new Color(.56f,.76f,.85f,.5f));
            b.onClick.AddListener(()=>action?.Invoke());return b;
        }
        public static Button BackButton(Transform parent,Action action,string name="Back",string label="Back")
        {
            var face=LabUIVisuals.Surface(parent,name,Color.white);
            var button=face.gameObject.AddComponent<Button>();button.targetGraphic=face;
            var colors=button.colors;colors.normalColor=Surface;colors.highlightedColor=Color.Lerp(Surface,Accent,.22f);
            colors.selectedColor=colors.highlightedColor;colors.pressedColor=Color.Lerp(Surface,Accent,.38f);colors.fadeDuration=0;button.colors=colors;
            var layout=face.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=6*TextScale;
            layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=layout.childControlHeight=true;
            layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            var ink=HighContrast?Color.white:Ink;float iconSize=24*TextScale;
            LabUIVisuals.Icon(face.transform,LabGlyph.Back,ink,iconSize);
            var caption=Text(face.transform,label,24,ink);caption.fontStyle=FontStyles.Bold;
            caption.textWrappingMode=TextWrappingModes.NoWrap;caption.alignment=TextAlignmentOptions.Center;
            float labelWidth=Mathf.Ceil(caption.GetPreferredValues(label).x)+2;Size(caption,width:labelWidth);
            float width=Mathf.Max(108,labelWidth+iconSize+layout.spacing+28);
            Size(face,BackHeight,width);face.rectTransform.sizeDelta=new Vector2(width,BackHeight);
            LabUIVisuals.Border(face.rectTransform);
            button.onClick.AddListener(()=>action());return button;
        }
        public static TMP_InputField Input(Transform parent,string placeholder,string value="",bool secret=false)
        {
            var image=LabUIVisuals.Surface(parent,placeholder,new Color(.018f,.031f,.043f));LabUIVisuals.Border(image.rectTransform);Size(image,Mathf.Max(64,40*TextScale+28));var input=image.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=image;input.textViewport=image.rectTransform;input.contentType=secret?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;input.characterLimit=254;
            var text=Text(image.transform,"",22);Fill(text.rectTransform,14);text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.NoWrap;input.textComponent=text;
            var hint=Text(image.transform,placeholder,22,Muted);Fill(hint.rectTransform,14);hint.alignment=TextAlignmentOptions.MidlineLeft;input.placeholder=hint;input.text=value;input.caretColor=Accent;input.selectionColor=new Color(.24f,.78f,.7f,.35f);return input;
        }
        public static RectTransform Scroll(Transform parent)
        {
            var rt=Rect("Scrollable content",parent);Fill(rt);var scroll=rt.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=Panel(rt,"Viewport",new Color(0,0,0,0));Fill(viewport.rectTransform);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Column(viewport.transform,"Content",28,14);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.offsetMin=Vector2.zero;content.offsetMax=Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport.rectTransform;scroll.content=content;
            var track=Panel(rt,"Scroll track",Surface);track.rectTransform.anchorMin=new Vector2(1,0);track.rectTransform.anchorMax=Vector2.one;track.rectTransform.pivot=Vector2.one;track.rectTransform.offsetMin=new Vector2(-8,6);track.rectTransform.offsetMax=new Vector2(-2,-6);
            var bar=track.gameObject.AddComponent<Scrollbar>();var thumb=Panel(track.transform,"Scroll position",Muted);Fill(thumb.rectTransform);bar.handleRect=thumb.rectTransform;bar.targetGraphic=thumb;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;viewport.rectTransform.offsetMax=new Vector2(-12,0);return content;
        }
        public static void Rule(Transform parent) { var p=Panel(parent,"Divider",new Color(.19f,.24f,.28f));Size(p,1);p.raycastTarget=false; }
        public static void Toggle(Transform parent,string label,Func<bool> get,Action<bool> set)
        {
            var b=Button(parent,(get()?"On   ":"Off   ")+label,null);b.onClick.AddListener(()=>{set(!get());b.GetComponentInChildren<TMP_Text>().text=(get()?"On   ":"Off   ")+label;});
        }
        public static void Cycle<T>(Transform parent,string label,T[] values,Func<T> get,Action<T> set)
        {
            var b=Button(parent,label+"   "+get(),null);b.onClick.AddListener(()=>{int i=Array.IndexOf(values,get());var value=values[(i+1)%values.Length];set(value);b.GetComponentInChildren<TMP_Text>().text=label+"   "+value;});
        }
        public static void Slider(Transform parent,string label,float min,float max,Func<float> get,Action<float> set,string format="0.00")
        {
            var caption=Text(parent,label+"   "+get().ToString(format),21,Muted);
            var rt=Rect(label,parent);Size(rt,44);var slider=rt.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;
            var track=Panel(rt,"Track",Surface);Fill(track.rectTransform);track.rectTransform.offsetMin=new Vector2(0,10);track.rectTransform.offsetMax=new Vector2(0,-10);
            var fill=Panel(track.transform,"Fill",Accent);Fill(fill.rectTransform);slider.fillRect=fill.rectTransform;
            // Slider drives both handle anchors; constrain its travel parent to keep the handle inside the row.
            var travel=Rect("Handle travel",rt);Fill(travel);travel.offsetMin=new Vector2(10,7);travel.offsetMax=new Vector2(-10,-7);
            var handle=LabUIVisuals.Surface(travel,"Handle",Ink);handle.rectTransform.sizeDelta=new Vector2(20,0);slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.value=get();slider.onValueChanged.AddListener(v=>{set(v);caption.text=label+"   "+v.ToString(format);});
        }
    }
}

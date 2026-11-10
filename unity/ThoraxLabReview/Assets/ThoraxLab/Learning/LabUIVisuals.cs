using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Learning
{
    public enum LabGlyph { Arrow, Back, Play, Pause, Check, Lock, Layers, Target, Assemble, Case, Tools, Settings, Mail, Hand, Info }

    // Small, consistent vector symbols. Anatomy pictures are separately baked from source meshes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabIcon : MaskableGraphic
    {
        public LabGlyph glyph;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            void Line(float ax,float ay,float bx,float by)
            {
                var r=rectTransform.rect;float side=Mathf.Min(r.width,r.height);var origin=r.center-Vector2.one*side*.5f;var a=origin+new Vector2(ax,ay)*side;var b=origin+new Vector2(bx,by)*side;
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*Mathf.Min(r.width,r.height)*.042f;
                int i=mesh.currentVertCount;mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
            }
            void Path(params float[] p){for(int i=2;i<p.Length;i+=2)Line(p[i-2],p[i-1],p[i],p[i+1]);}
            void Box(float a,float b,float c,float d){Path(a,b,c,b,c,d,a,d,a,b);}
            void Arc(float cx,float cy,float radius,float from,float to,int count=18){for(int i=0;i<count;i++){float a=Mathf.Lerp(from,to,(float)i/count)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/count)*Mathf.Deg2Rad;Line(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius,cx+Mathf.Cos(b)*radius,cy+Mathf.Sin(b)*radius);}}
            switch(glyph)
            {
                case LabGlyph.Arrow:Path(.18f,.5f,.82f,.5f,.58f,.74f);Line(.82f,.5f,.58f,.26f);break;
                case LabGlyph.Back:Path(.82f,.5f,.18f,.5f,.42f,.74f);Line(.18f,.5f,.42f,.26f);break;
                case LabGlyph.Play:Path(.3f,.2f,.78f,.5f,.3f,.8f,.3f,.2f);break;
                case LabGlyph.Pause:Line(.34f,.22f,.34f,.78f);Line(.66f,.22f,.66f,.78f);break;
                case LabGlyph.Check:Path(.16f,.48f,.4f,.25f,.84f,.77f);break;
                case LabGlyph.Lock:Box(.23f,.15f,.77f,.56f);Arc(.5f,.57f,.2f,0,180);Line(.5f,.29f,.5f,.41f);break;
                case LabGlyph.Layers:Path(.13f,.63f,.5f,.84f,.87f,.63f,.5f,.42f,.13f,.63f);Path(.13f,.43f,.5f,.22f,.87f,.43f);Path(.13f,.25f,.5f,.06f,.87f,.25f);break;
                case LabGlyph.Target:Arc(.5f,.5f,.29f,0,360);Line(.5f,.05f,.5f,.32f);Line(.5f,.68f,.5f,.95f);Line(.05f,.5f,.32f,.5f);Line(.68f,.5f,.95f,.5f);break;
                case LabGlyph.Assemble:Box(.12f,.12f,.43f,.43f);Box(.57f,.12f,.88f,.43f);Box(.12f,.57f,.43f,.88f);Path(.6f,.61f,.83f,.73f,.72f,.94f,.5f,.83f,.6f,.61f);break;
                case LabGlyph.Case:Box(.2f,.15f,.8f,.84f);Box(.36f,.78f,.64f,.94f);Line(.35f,.56f,.65f,.56f);Line(.5f,.42f,.5f,.7f);Line(.34f,.29f,.66f,.29f);break;
                case LabGlyph.Tools:Path(.16f,.13f,.69f,.66f,.78f,.58f,.23f,.06f,.16f,.13f);Path(.69f,.66f,.79f,.88f,.93f,.93f,.86f,.72f,.78f,.58f);Line(.29f,.2f,.42f,.33f);break;
                case LabGlyph.Settings:for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(.5f+Mathf.Cos(a)*.3f,.5f+Mathf.Sin(a)*.3f,.5f+Mathf.Cos(a)*.43f,.5f+Mathf.Sin(a)*.43f);}Arc(.5f,.5f,.29f,0,360);Arc(.5f,.5f,.1f,0,360);break;
                case LabGlyph.Mail:Box(.12f,.23f,.88f,.78f);Path(.12f,.76f,.5f,.45f,.88f,.76f);break;
                case LabGlyph.Hand:Path(.27f,.15f,.12f,.43f,.23f,.52f,.35f,.37f,.35f,.8f,.45f,.84f,.46f,.5f,.48f,.91f,.58f,.91f,.59f,.5f,.63f,.81f,.73f,.78f,.7f,.45f,.77f,.65f,.86f,.6f,.81f,.31f,.68f,.13f,.27f,.15f);break;
                default:Arc(.5f,.5f,.36f,0,360);Line(.5f,.23f,.5f,.55f);Line(.5f,.68f,.5f,.72f);break;
            }
        }
    }

    public static class LabUIVisuals
    {
        static Sprite rounded,outline;
        static readonly Dictionary<string,Texture2D> artwork=new();
        public static Sprite Rounded => rounded?rounded:rounded=MakeSurface(false);
        public static Sprite Outline => outline?outline:outline=MakeSurface(true);
        static Sprite MakeSurface(bool ring)
        {
            const int size=64;const float radius=15;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=ring?"Console edge mask":"Rounded control mask",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var q=new Vector2(Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius),Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius));
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                float alpha=ring?Mathf.Clamp01(.6f-Mathf.Abs(d+1.15f)):Mathf.Clamp01(.5f-d);
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);return Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*17);
        }
        public static Image Surface(Transform parent,string name,Color color)
        {var image=LabWidgets.Panel(parent,name,color);image.sprite=Rounded;image.type=Image.Type.Sliced;return image;}
        public static void Border(RectTransform parent,Color? color=null)
        {var border=LabWidgets.Panel(parent,"Fine console edge",color??new Color(.24f,.32f,.36f,.7f));LabWidgets.Fill(border.rectTransform);border.sprite=Outline;border.type=Image.Type.Sliced;border.raycastTarget=false;var layout=border.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;}
        public static LabIcon Icon(Transform parent,LabGlyph glyph,Color? color=null,float size=26)
        {var rt=LabWidgets.Rect(glyph+" symbol",parent);rt.gameObject.AddComponent<CanvasRenderer>();var icon=rt.gameObject.AddComponent<LabIcon>();icon.glyph=glyph;icon.color=color??LabWidgets.Muted;icon.raycastTarget=false;LabWidgets.Size(icon,size,size);return icon;}
        public static Texture2D Art(string key)
        {if(!artwork.TryGetValue(key,out var texture)){texture=Resources.Load<Texture2D>("Learning/CardArt/"+key);artwork[key]=texture;}return texture;}
        public static RectTransform Hero(Transform parent,string key,float height=184)
        {
            var card=Surface(parent,"Anatomy study image",new Color(.022f,.036f,.046f));LabWidgets.Size(card,height);var frame=LabWidgets.Rect("Image frame",card.transform);LabWidgets.Fill(frame,2);Picture(frame,key);
            Border(card.rectTransform);return card.rectTransform;
        }
        static void Picture(RectTransform frame,string key)
        {
            var picture=LabWidgets.Rect("Source specimen render",frame);LabWidgets.Fill(picture);
            var image=picture.gameObject.AddComponent<RawImage>();image.texture=Art(key);image.color=image.texture?Color.white:Color.clear;image.raycastTarget=false;
            var ratio=picture.gameObject.AddComponent<AspectRatioFitter>();ratio.aspectMode=AspectRatioFitter.AspectMode.FitInParent;ratio.aspectRatio=image.texture?(float)image.texture.width/image.texture.height:1.6f;
        }
        public static Button Card(Transform parent,string title,string description,string artKey,System.Action action,string actionName=null,string status=null,bool enabled=true,LabGlyph symbol=LabGlyph.Arrow)
        {
            var card=Surface(parent,actionName??title,LabWidgets.Surface);var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;button.interactable=enabled;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.77f,.96f,.94f);colors.pressedColor=new Color(.55f,.77f,.75f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.55f,.58f,.61f);colors.fadeDuration=.12f;button.colors=colors;
            var layout=card.gameObject.AddComponent<HorizontalLayoutGroup>();layout.padding=new RectOffset(16,20,16,16);layout.spacing=20;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.MiddleLeft;
            var size=LabWidgets.Size(card,132);size.preferredHeight=-1;
            if(!string.IsNullOrEmpty(artKey))
            {
                var picture=LabWidgets.Rect("Anatomy preview",card.transform);LabWidgets.Size(picture,104,150);Picture(picture,artKey);
            }
            else Icon(card.transform,symbol,LabWidgets.Accent,32);
            var body=LabWidgets.Column(card.transform,"Activity details",0,5);LabWidgets.Size(body,flexible:1);
            var name=LabWidgets.Text(body,title,28);name.fontStyle=FontStyles.Bold;
            if(!string.IsNullOrEmpty(description))LabWidgets.Text(body,description,21,LabWidgets.Muted);
            if(!string.IsNullOrEmpty(status))LabWidgets.Text(body,status,20,enabled?LabWidgets.Accent:LabWidgets.Muted);
            Icon(card.transform,enabled?LabGlyph.Arrow:LabGlyph.Lock,enabled?LabWidgets.Accent:LabWidgets.Muted,24);
            Border(card.rectTransform);button.onClick.AddListener(()=>action?.Invoke());return button;
        }
        public static LabGlyph ButtonGlyph(string text)
        {
            var t=text.ToLowerInvariant();if(t.Contains("back")||t.Contains("return")||t.Contains("cancel"))return LabGlyph.Back;
            if(t.Contains("pause"))return LabGlyph.Pause;if(t.Contains("setting")||t.Contains("profile")||t=="menu")return LabGlyph.Settings;
            if(t.Contains("tool")||t.Contains("scalpel")||t.Contains("saw"))return LabGlyph.Tools;if(t.Contains("email")||t.Contains("sign in"))return LabGlyph.Mail;
            if(t.Contains("finish")||t=="done"||t.Contains("apply"))return LabGlyph.Check;if(t.Contains("layer"))return LabGlyph.Layers;
            if(t.Contains("lesson")||t.Contains("learn")||t.Contains("show me")||t.Contains("practice"))return LabGlyph.Play;return LabGlyph.Arrow;
        }
    }
}

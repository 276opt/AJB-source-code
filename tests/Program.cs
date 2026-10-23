using System;
using System.Linq;
using AccurateJudgementBar;
using UnityEngine;
public enum Difficulty { Lenient, Normal, Strict }
namespace UnityEngine {
    public partial struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1) {this.r=r;this.g=g;this.b=b;this.a=a;} }
    public static partial class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); public static float Round(float a)=>(float)Math.Round(a); public static float Clamp(float a,float b,float c)=>Math.Clamp(a,b,c); }
}
namespace AccurateJudgementBar {
    public class Settings {
        public bool ShowXPerfectJudgement = true;
        public bool VanillaXPerfectStyle = false;
        public bool VanillaXPerfectOutline = true;
        public int XPerfectOutlineWidth = 1;
    }
    public static class Main { public static Settings settings = new Settings(); public static Entry? Mod; public class Entry { public Log Logger=new Log(); } public class Log { public void Error(string s){} } }
    public static class MeterWatcher {
        public static float DiffToMinimum(Difficulty d)=>d==Difficulty.Lenient?91:d==Difficulty.Strict?40:65;
        public static Color GetRed()=>new Color(1,0,0); public static Color GetOrange()=>new Color(1,.5f,0); public static Color GetYellow()=>new Color(1,1,0); public static Color GetGreen()=>new Color(0,1,0); public static Color GetBlue()=>NativeXPerfect.Color;
    }
}
class Program {
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Main(string[] args){
        if(args.Length == 1 && args[0] == "--localization") { LocalizationTests.Run(); return; }
        bool preview = args.Length == 1 && args[0] == "--judgement-preview";
        bool judgementOnly = preview || (args.Length == 1 && args[0] == "--judgement");
        if(args.Length > 0 && !judgementOnly) { GameMetadata.Inspect(args[0]); return; }
        if (!judgementOnly) LocalizationTests.Run();
        GameCompatibilityTests.Run();
        NativeXPerfectTests.Run();
        JudgementBandAnimationTests.Run();
        NativeDisplayTests.Run();
        OutlineMeshTests.Run(preview);
        VanillaXPerfectStyleTests.Run();
        foreach(var d in new[]{Difficulty.Lenient,Difficulty.Normal,Difficulty.Strict})
        foreach(float bpm in new[]{100f,219f,307f,400f,500f,1000f})
        foreach(float speed in new[]{.5f,1f,1.5f,2f}) {
            var c=new JudgementContext(bpm,d,speed); var bands=JudgementApi.Evaluate(c);
            float Legacy(float a,float b,float min=25)=>Math.Max(Math.Max(60000/bpm*a/180,b)/speed,min);
            float reference=Legacy(60,MeterWatcher.DiffToMinimum(d));
            Check(bands.Single(b=>b.Id=="red").Width==316,"red");
            Check(bands.Single(b=>b.Id=="orange").Width==300,"orange");
            Check(bands.Single(b=>b.Id=="yellow").Width==Math.Round(300*(Legacy(45,30)/reference)),"yellow has no extra pixel");
            Check(bands.Single(b=>b.Id=="green").WindowMs==Legacy(30,20),"green regression");
            var fixedBands=JudgementApi.Evaluate(c,false);
            Check(fixedBands.Single(b=>b.Id=="orange").Width==300,"fixed orange");
            Check(fixedBands.Single(b=>b.Id=="yellow").Width==225,"fixed yellow");
            Check(fixedBands.Single(b=>b.Id=="green").Width==150,"fixed green");
            Check(fixedBands.Single(b=>b.Id=="red").Width==316,"fixed red border");
            Check(!fixedBands.Any(b=>b.Id=="blue"),"fixed blue off");
            Check(fixedBands.All(b=>b.WindowMs==bands.Single(a=>a.Id==b.Id).WindowMs),"fixed mode preserves windows");
            Check(JudgementApi.Evaluate(c,true).All(b=>b.Width==bands.Single(a=>a.Id==b.Id).Width),"accurate widths restored");
        }
        var context=new JudgementContext(400,Difficulty.Normal);
        Check(!JudgementApi.Evaluate(context).Any(b=>b.Id=="blue"),"blue off");
        Persistence.enableCompetitiveMode=true;
        Persistence.hitMarginPerfectTick=HitMarginPerfectTickPreset.ShowXPerfect;
        Check(JudgementApi.Evaluate(context).Any(b=>b.Id=="blue"),"blue on");
        Check(JudgementApi.Evaluate(context,false).Single(b=>b.Id=="blue").Width==62.5f,"fixed native XPerfect on");
        var custom=new JudgementDefinition("test.perfect",c=>5,_=>new Color(1,1,1));
        JudgementApi.Register(custom);
        Check(JudgementApi.Evaluate(context).Last().Id==custom.Id,"custom sorted");
        Check(JudgementApi.Evaluate(context,false).Single(b=>b.Id==custom.Id).Width==JudgementApi.Evaluate(context).Single(b=>b.Id==custom.Id).Width,"fixed mode preserves custom bands");
        bool duplicate=false;try{JudgementApi.Register(custom);}catch(ArgumentException){duplicate=true;}
        Check(duplicate,"duplicate rejected");
        JudgementApi.Replace(new JudgementDefinition(custom.Id,c=>10,_=>new Color()));
        Check(JudgementApi.Evaluate(context).Last().WindowMs==10,"replace");
        JudgementApi.Replace(new JudgementDefinition(custom.Id,c=>float.NaN,_=>new Color()));
        Check(!JudgementApi.Evaluate(context).Any(b=>b.Id==custom.Id),"NaN isolated");
        JudgementApi.Replace(new JudgementDefinition(custom.Id,c=>throw new Exception(),_=>new Color()));
        Check(JudgementApi.Evaluate(context).Count==5,"throw isolated");
        Check(JudgementApi.Remove(custom.Id),"remove");
        bool invalid=false;try{new JudgementContext(0,Difficulty.Normal);}catch(ArgumentOutOfRangeException){invalid=true;}
        Check(invalid,"invalid context");
        var animation = new FailureBandAnimation();
        Check(animation.Sample(false,true,0,.4f)==0,"disabled even with a visibility trigger");
        Check(animation.Sample(true,false,0,.4f)==0,"no trigger stays hidden");
        Check(animation.Sample(true,true,1,.4f)==0,"expansion starts at zero");
        Check(Math.Abs(animation.Sample(true,true,1.2,.4f)-.875f)<.00001,"expansion ease out midpoint");
        Check(animation.Sample(true,true,2,.4f)==1,"expansion completes");
        Check(animation.Sample(true,false,3,.4f)==1,"retraction starts at current width");
        Check(Math.Abs(animation.Sample(true,false,3.2,.4f)-.125f)<.00001,"retraction ease out midpoint");
        Check(animation.Sample(true,false,4,.4f)==0,"retraction completes");
        Check(animation.Sample(true,true,5,0)==1,"zero duration immediate");
        Check(animation.Sample(false,true,6,.4f)==0,"limit hides even after expansion");
        Check(animation.Sample(true,true,7,.4f)==0,"fresh expansion after suppression");
        float partial = animation.Sample(true,true,7.1,.4f);
        Check(partial > 0 && partial < 1 && animation.Sample(true,false,7.1,.4f)==partial,
            "leaving a short safe section reverses without a jump");
        float shrinking = animation.Sample(true,false,7.2,.4f);
        Check(shrinking > 0 && shrinking < partial && animation.Sample(true,true,7.2,.4f)==shrinking,
            "re-entering during retraction reverses without a jump");
        Check(animation.Sample(true,true,8,.4f)==1,"reversed animation finishes");
        animation.Reset();
        Check(animation.Sample(true,false,9,.4f)==0,"retry clears immediately");
        foreach (float duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            Check(animation.Sample(true,true,10,duration)==1,"invalid/zero duration shows immediately");
            Check(animation.Sample(true,false,11,duration)==0,"invalid/zero duration retracts immediately");
        }
        Check(animation.Sample(true,false,12,.4f)==0,"restoring animation cannot revive an old transition");
        Console.WriteLine($"Passed {checks} checks (API source with Unity/game stubs; no rendering test).");
    }
}

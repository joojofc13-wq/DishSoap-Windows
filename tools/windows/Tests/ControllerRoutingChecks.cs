using Dishwasher;

internal static class ControllerRoutingChecks
{
    private static void Check(bool ok,string name)
    { if(!ok) throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name); }
    public static void Run()
    {
        Check(!new DesktopSettings().LocalCoop,"single-player input is the default");
        Check(new DesktopSettings().ControllerVibration,"vibration remains enabled by default for existing players");
        var originalSettings=DesktopInput.Settings;
        try
        {
            DesktopInput.Settings=new DesktopSettings();
            int calls=0,slot=-1;float left=-1,right=-1;
            bool Send(Microsoft.Xna.Framework.PlayerIndex index,float l,float r) { calls++;slot=(int)index;left=l;right=r;return true; }
            Check(DesktopInput.SendVibration(2,.7f,.4f,Send)&&slot==2&&left==.7f&&right==.4f,"enabled vibration retains motor strength and the mapped physical slot");
            DesktopInput.Settings.ControllerVibration=false;
            Check(DesktopInput.SendVibration(2,.7f,.4f,Send)&&left==0&&right==0,"disabled vibration sends an explicit stop for both motors");
            Check(!DesktopInput.SendVibration(-1,1,1,Send)&&!DesktopInput.SendVibration(4,1,1,Send)&&calls==2,"unmapped players never address vibration hardware");
            DesktopInput.Settings.Save();
            Check(!DesktopSettings.Load().ControllerVibration,"vibration opt-out survives saving and reloading settings");
        }
        finally { DesktopInput.Settings=originalSettings; }
        Check(!DesktopInput.HasActivity(default),"idle controller state does not claim ownership");
        Check(DesktopInput.HasActivity(new Microsoft.Xna.Framework.Input.GamePadState(Microsoft.Xna.Framework.Vector2.Zero,Microsoft.Xna.Framework.Vector2.Zero,0,0,Microsoft.Xna.Framework.Input.Buttons.A)),"a real button press claims controller ownership");
        for(int slot=0;slot<4;slot++)
        {
            var route=new DesktopControllerRouting();var connected=new bool[4];connected[slot]=true;
            route.Update(connected,false);
            Check(route.Resolve(0)==slot&&Enumerable.Range(1,3).All(i=>route.Resolve(i)==-1),"one controller in physical slot "+slot+" belongs only to player one");
        }
        var router=new DesktopControllerRouting();
        router.Update(new[]{false,false,true,false},false);
        router.Update(new[]{true,false,true,false},false);
        Check(router.Resolve(0)==2&&router.Resolve(1)==-1,"a newly detected duplicate slot cannot steal primary ownership or join another player in solo mode");
        router.Update(new[]{true,false,false,false},false);
        Check(router.Resolve(0)==0,"reconnection in another physical slot keeps logical player one");
        router.Update(new[]{true,false,true,false},true);
        Check(router.Resolve(0)==0&&router.Resolve(1)==2,"explicit co-op assigns a distinct physical controller to player two");
        router.Update(new[]{false,false,true,false},true);
        Check(router.Resolve(0)==-1&&router.Resolve(1)==2,"disconnecting the primary controller does not copy player two's controller into player one");
        router.Update(new[]{true,false,true,true},true);
        var assigned=Enumerable.Range(0,4).Select(router.Resolve).Where(i=>i>=0).ToArray();
        Check(assigned.Distinct().Count()==assigned.Length,"co-op never assigns one physical slot to multiple logical players");
        router.Update(new[]{true,false,true,true},false);
        Check(router.Resolve(0)==0&&router.Resolve(1)==-1&&router.Resolve(2)==-1,"disabling co-op disconnects additional logical players");
        Check(router.Resolve(-1)==-1&&router.Resolve(4)==-1,"invalid player indices do not address a controller");
        var idle=new DesktopControllerRouting();
        idle.Update(new[]{true,true,false,false},false,new[]{false,false,false,false});
        idle.Update(new[]{true,true,false,false},false,new[]{false,true,false,false});
        Check(idle.Resolve(0)==1&&idle.Resolve(1)==-1,"the active controller claims player one ahead of an idle virtual device");
        idle.Update(new[]{true,true,false,false},false,new[]{true,false,false,false});
        Check(idle.Resolve(0)==1,"later input on another slot cannot steal the established primary controller");
        idle.Update(new[]{true,false,false,false},false,new[]{false,false,false,false});
        idle.Update(new[]{true,false,false,true},false,new[]{false,false,false,true});
        Check(idle.Resolve(0)==3,"reconnected active controller reclaims player one after its old slot disconnects");
        var coop=new DesktopControllerRouting();
        coop.Update(new[]{true,false,true,false},true,new[]{true,false,false,false});
        coop.Update(new[]{false,false,true,false},true,new[]{false,false,true,false});
        Check(coop.Resolve(0)==-1&&coop.Resolve(1)==2,"active player two is not stolen after an established primary disconnects");
    }
}

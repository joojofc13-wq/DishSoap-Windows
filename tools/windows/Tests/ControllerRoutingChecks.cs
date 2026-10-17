using Dishwasher;

internal static class ControllerRoutingChecks
{
    private static void Check(bool ok,string name)
    { if(!ok) throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name); }
    public static void Run()
    {
        Check(!new DesktopSettings().LocalCoop,"single-player input is the default");
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
    }
}

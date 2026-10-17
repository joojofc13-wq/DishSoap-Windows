using System;

namespace Dishwasher
{
    // Logical player numbers stay separate from SDL's reusable device slots.
    public sealed class DesktopControllerRouting
    {
        private readonly int[] _physical={-1,-1,-1,-1};
        public int Resolve(int logical)=>logical>=0&&logical<_physical.Length?_physical[logical]:-1;
        public void Update(bool[] connected,bool localCoop)
        {
            if(connected==null||connected.Length!=4) throw new ArgumentException("Four physical controller slots are required.");
            for(int i=0;i<4;i++)
                if((i>0&&!localCoop)||(_physical[i]>=0&&!connected[_physical[i]])) _physical[i]=-1;
            for(int logical=0;logical<(localCoop?4:1);logical++)
            {
                if(_physical[logical]>=0) continue;
                for(int physical=0;physical<4;physical++)
                {
                    if(!connected[physical]||Array.IndexOf(_physical,physical)>=0) continue;
                    _physical[logical]=physical;break;
                }
            }
        }
    }
}

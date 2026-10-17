using System;

namespace Dishwasher
{
    // Logical player numbers stay separate from SDL's reusable device slots.
    public sealed class DesktopControllerRouting
    {
        private readonly int[] _physical={-1,-1,-1,-1};
        private bool _primaryClaimed,_primaryEstablished;
        public int Resolve(int logical)=>logical>=0&&logical<_physical.Length?_physical[logical]:-1;
        public void Update(bool[] connected,bool localCoop,bool[] active=null)
        {
            if(connected==null||connected.Length!=4) throw new ArgumentException("Four physical controller slots are required.");
            if(active!=null&&active.Length!=4) throw new ArgumentException("Four activity flags are required.");
            if(_physical[0]>=0&&!connected[_physical[0]]) _primaryClaimed=false;
            for(int i=0;i<4;i++)
                if((i>0&&!localCoop)||(_physical[i]>=0&&!connected[_physical[i]])) _physical[i]=-1;
            // An idle virtual device must not reserve player one ahead of the
            // controller the player actually uses. Lock ownership on activity.
            if(!_primaryClaimed&&active!=null)
            {
                int claim=-1;
                if(_physical[0]>=0&&active[_physical[0]]) claim=_physical[0];
                else for(int i=0;i<4;i++)
                {
                    int owner=Array.IndexOf(_physical,i);
                    if(connected[i]&&active[i]&&(!_primaryEstablished||owner<=0)) { claim=i;break; }
                }
                if(claim>=0)
                {
                    for(int i=1;i<4;i++) if(_physical[i]==claim) _physical[i]=-1;
                    _physical[0]=claim;_primaryClaimed=_primaryEstablished=true;
                }
            }
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

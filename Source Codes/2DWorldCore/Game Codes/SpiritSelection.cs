using System.Windows;
using Farseer.Xna.Framework;
using FarseerPhysics.Dynamics;
using Core.Data.Entity;
using Core.Data;


namespace _2DWorldCore
{
    public class SpiritSelection
    {
        private Spirit _selectedSpirit;


        public SpiritSelection()  { }


        #region Methods


        public void SetSelectedSpirit(Spirit sp)
        {
            _selectedSpirit = sp;
        }


       
        /// <summary>
        /// Get creature hit on specified position.
        /// </summary>
        public Spirit GetCreature(Body hit, Vector2 worldPos, Level level)
        {
            if (level == null)
            {
                return null;
            }

    
            Spirit spOut;

            //check if we picked main body
            if ( Level.Instance.MapBodyToSpirits.TryGetValue(hit, out spOut ))
            {
                return spOut;
            }

            //TOOD better walk the jointed graph ?, this is to slow or requiring picking main obyd
            //we picked something, see if its inside a spirt (slow  linear time search )
            if (level.Entities != null)
            {
                foreach (Spirit c in level.GetSpiritEntities())
                {
               
                    if (c.AABB.Contains(ref worldPos) == true)  // aabb first
                    {
                        // all bodies next
                        foreach (Body b in c.Bodies)
                        {
                            if (b == hit)
                            {
                                return c;
                            }
                        }
                    }
                }
            }

            return null;
        }


        /*
        // get all available creature on specified rectangular area
        public static List<Spirit> GetCreatureAtArea(IEnumerable<Spirit> availableCreatures,
            float minX, float minY, float maxX, float maxY)
        {
            return GetCreatureAtArea(availableCreatures, new AABB(
                new Vector2(minX, minY), new Vector2(maxX, maxY)));
        }

        // get all available creature on specified rectangular area
        public static List<Spirit> GetCreatureAtArea(IEnumerable<Spirit> availableCreatures,
                                                     AABB aabb)
        {
            List<Spirit> selectedCreature = new List<Spirit>();
            foreach (Spirit c in availableCreatures)
            {
                // TODO: perhaps intersect is not enough, should be completely inside

                if (AABB.Intersect(c.AABB, aabb) == true)
                {
                    selectedCreature.Add(c);
                }
            }
            return selectedCreature;
        }
        */


        #endregion
    }
}

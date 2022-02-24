using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;


using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace _2DWorldCore.UI
{
    public class SettingsDlg: CloseableDialogBox
    {

        public SettingsDlg(AABB bounds = default ) : base(bounds)
        {

           
            const float itemHeight = 64;

            const float itemMarginY = 4;

            const float yMargin = 10;

            const float xMargin = 10;

            Farseer.Xna.Framework.Vector2 pos = new Farseer.Xna.Framework.Vector2(xMargin, yMargin);

            Farseer.Xna.Framework.Vector2 posIncrement = new Farseer.Xna.Framework.Vector2(0, itemHeight+ itemMarginY);

            foreach (var x in  CoreGame.SwitchLabels )
            {
                //tag and name them with the english version.. label can be changed by xlate
                AddElement(new CheckBox1(new AABB(itemHeight, itemHeight, pos += posIncrement), x,x));
            }


            foreach (var x in CoreGame.FPSvals)
            {
                //tag and name them with the english version.. label can be changed by xlate
                AddElement(new CheckBox1(new AABB(itemHeight, itemHeight, pos += posIncrement), x, x));
            }
          
  
        }

    }

}

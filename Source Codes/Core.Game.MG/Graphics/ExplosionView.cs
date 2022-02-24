 using  Core.Game.MG.Drawing;
using Core.Game.MG.Graphics;
using Farseer.Xna.Framework;
using Core.Data.Animations;
using VisibilityPolygon;
using System.Collections.Generic;
using Core.Data.Entity;
using FarseerPhysics.Collision;
using System.Collections;
using FarseerPhysics.Dynamics;
using System;
using Core.Game.MG.Simulation;
using FarseerPhysics.Dynamics.Particles;
using FarseerPhysics.Factories;

namespace Core.Game.MG.Graphics
{
    public class ExplosionView
    {
        //TODO maek a gradient radiall shader and circ
       //if clip shader easy then dont cahgne the vportshadow we gotits ok for now


        //TODO make it static..

        //use a body for this i think.. its basicallyh a body with the gradient brush, no needed for the view data.
        //
        //polygon view in the DL will just be a world Polygon see mg extended.
  

        //TODO CODE REVIEW REPEAT With bomb...
        //LETS ADD A BodyColor.White { get  or something to return all this.. LIKE WINDOWS STATIC COLORS
        //TODO pass these colors in so we can tweak it in tool.
        static BodyColor _white1 = new BodyColor(255, 255, 255, 255);
        static BodyColor _yellow = new BodyColor(255, 255, 0, 255);
        static BodyColor _blue = new BodyColor(0, 0, 255, 255);//not used..
        static BodyColor _orange = new BodyColor(249, 64, 9, 255);
        static BodyColor _redOrange = new BodyColor(255, 24, 9, 255);
        static BodyColor _blueTransparent = new BodyColor(0, 0, 100, 0);  //.. seems like a black edge.. better if we can just face it to nothing

        //  static BodyColor _transparent = new BodyColor(0, 0, 0, 0); //this caused a black ring on outer edge
      //TODO a better description? graphics_mg

     //   static double _2WhiteStart = 0.1d;
     //   static double _2WhiteEnd = 0.8d;
       // static double _3YellowStart = 0.2d;
      //  static double _3YellowEnd = 0.85d;
        //static double _4OrangeStart = 0.6d;
       // static double _4OrangeEnd = 0.97d;

        //consiver using SVG assets for this . and the reader inNgraphics.
        //TODO this looks probably more complex that is needed .. the first two whites are the same for example.   was done in Expression then converted to code
        /// <summary>
        /// Create an explosive view with white to red gradients.
        /// </summary>
        /// <param name="position">position in WCS</param>
        /// <param name="radius">radius of full body</param>
        /// <param name="clipGeometry">clip inside this path optional</param>
        /// <returns>The body representing the explosive flash of lit gas  and /or shock wave </returns>
        public static Body AddPolygonExplosionBody(Vector2 position, float radius, bool blueOuter, bool isRadial = false)
        {


            Body viewBody;  //dummy body.. static.. just used to map a view to.. 
            viewBody = new Body(World.Instance);
            viewBody.Position = position;
            viewBody.Info = BodyInfo.KeepVisible;  // this might be needed only since AABB is invalid ( TODO fix AABB using radius and remove)
            viewBody.BodyType = BodyType.Dynamic;  //this is a dummy body.. just because or graphics map views to a body to access them.
            viewBody.IsNotCollideable = true;


        /*   TODO MG_GRAPHICS make some kind of immediate view for this..
         *   
         *   Particle viewBody = new Particle(World.Instance); //dummy body.. static.. just used to map a view to.. 

            FixtureFactory.CreateCircle(radius, WindDrag.DefaultAirDensity, viewBody);

            viewBody.Position = position;
            viewBody.Info = BodyInfo.KeepVisible;
            viewBody.BodyType = BodyType.Dynamic;

         //   viewBody.IsNotCollideable = true;  do this later
          */



            BodyGradientBrush gbrush = null;

            if (isRadial)
            {
                BodyRadialGradientBrush brush = new BodyRadialGradientBrush();
                brush.Center = new Vector2(0.5f, 0.5f);  //why not centerd on 0,0?  tried it doesnt work,   circle is like ellipse.
                brush.GradientOrigin = new Vector2(0.5f, 0.5f);
                brush.RadiusX = 0.5f;
                brush.RadiusY = 0.5f;
                gbrush = brush;
            }else 
            {
                BodyLinearGradientBrush lbrush = new BodyLinearGradientBrush();
                lbrush.StartPoint= new Vector2(-0.5f, -0.5f);
                lbrush.EndPoint = new Vector2(0.5f, 0.5f);
                gbrush = lbrush;
            }

            viewBody.GradientBrush = gbrush;

            viewBody.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_white1, 0d));
            viewBody.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_white1, 0.2d));
            viewBody.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_yellow, 0.5d));
            viewBody.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_orange, 0.57d));

            if (blueOuter)
            {
                viewBody.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_blueTransparent, 0.97d));
            }
            else
            {
                viewBody.GradientBrush.GradientStops.Add(new BodyBrushGradientStop(_redOrange, 0.65d));
            }


            //TODO .. we need to see this tho
            viewBody.Color = BodyColor.Transparent;

            viewBody.EdgeStrokeColor = BodyColor.CoolCopper;

            //HIDE FOR NOW
//#if PRODUCTION
       //     viewBody.EdgeStrokeColor = BodyColor.Transparent;
//#endif

            // this is to remove white view on start of explosion
            UpdateGradientBrush(viewBody.GradientBrush);

            return viewBody;
        }



        static void UpdateGradientBrush(BodyGradientBrush brush)
        {

            // TODO: might need common converter for BodyBrush -> GradientBrush later
#if GRAPHICS_MG
      if (brush is BodyLinearGradientBrush)
            {
                BodyLinearGradientBrush bodyBrush = brush as BodyLinearGradientBrush;
                LinearGradientBrush lBrush = this.Fill as LinearGradientBrush;
                if (lBrush == null)
                {
                    lBrush = new LinearGradientBrush();
                }

                lBrush.StartVector2 = ShapeUtility.VectorToPoint(bodyBrush.StartVector2);
                lBrush.EndVector2 = ShapeUtility.VectorToPoint(bodyBrush.EndVector2);

                UpdateLinearGradientStops(lBrush.GradientStops, bodyBrush) ;

                if (this.Fill != lBrush)
                    this.Fill = lBrush;


            }
            else if (brush is BodyRadialGradientBrush)
            {

#endif
#if (RADIAL_GRADIENT)
                BodyRadialGradientBrush bodyBrush = brush as BodyRadialGradientBrush;
                RadialGradientBrush rBrush = this.Fill as RadialGradientBrush;
                if (rBrush == null)
                {
                    rBrush = new RadialGradientBrush();
                }

                rBrush.Center = ShapeUtility.VectorToPoint(bodyBrush.Center);
                rBrush.GradientOrigin = ShapeUtility.VectorToPoint(bodyBrush.GradientOrigin);

                rBrush.RadiusX = bodyBrush.RadiusX;
                rBrush.RadiusY = bodyBrush.RadiusY;

               UpdateGradientStops(rBrush.GradientStops, bodyBrush);


                if (this.Fill != rBrush)
                    this.Fill = rBrush;

#else
                //  Debug.WriteLine("not implemented radial brush in winrt");
                return;
#endif
          
  
    }

#if RADIAL_GRADIENT
        void UpdateGradientStops(IObservableVector<GradientStop> gradientStops, BodyGradientBrush brush)
        {


            Debug.Assert(gradientStops != null);


            if (gradientStops != null &&
                gradientStops.Count == brush.GradientStops.Count)
            {
                int max = gradientStops.Count;
                for (int i = 0; i < max; i++)
                {
                    gradientStops[i].Color = HSBColor.ToMediaColor(brush.GradientStops[i].Color);
                    gradientStops[i].Offset = brush.GradientStops[i].Offset;
                }
            }else
            {
                gradientStops.Clear();
                for (  int i = 0; i < brush.GradientStops.Count; i++)
                {

                    GradientStop stop = new GradientStop();
                    stop.Color = HSBColor.ToMediaColor(brush.GradientStops[i].Color);
                    stop.Offset = brush.GradientStops[i].Offset;

                    gradientStops.Add(stop);
              
                }
                 
                    
           }

        }

#endif

#if GRAPHICS_MG
        //The radial brush has a newer implementation that is not compatible, using IObservableVector<GradientStop> which doesnt convert to GradientStopCollection
        //so without a more general converter, just repeating the code for now, this is all  slow UI code and will be replaced with monogame dazzles
        //
        void UpdateLinearGradientStops(GradientStopCollection gradientStops, BodyGradientBrush brush)
        {

            // try reuse first
            if (gradientStops != null &&
                gradientStops.Count == brush.GradientStops.Count)
            {
                int max = gradientStops.Count;
                for (int i = 0; i < max; i++)
                {
                    gradientStops[i].Color = HSBColor.ToMediaColor(brush.GradientStops[i].Color);
                    gradientStops[i].Offset = brush.GradientStops[i].Offset;
                }
            }
            else
            {
                GradientStopCollection stops = new GradientStopCollection();
                foreach (BodyBrushGradientStop stop in brush.GradientStops)
                {
                    stops.Add(CreateNewGradientStop(stop.Color, stop.Offset));
                }
                gradientStops = stops;
            }
        }


        private GradientStop CreateNewGradientStop(BodyColor bcolor, double offset)
        {
            GradientStop gs = new GradientStop();
            gs.Color = HSBColor.ToMediaColor(bcolor);
            gs.Offset = offset;
            return gs;
        }


        //move out the gradients... after radius of bomb was implemented , not sure if this is needed
        static public void AnimateBombBodyGradientStop(Body body, double fractionExplosionLifeTime)
        {
            body.GradientBrush.GradientStops[1].Offset = _2WhiteStart + (fractionExplosionLifeTime * (_2WhiteEnd - _2WhiteStart));
            body.GradientBrush.GradientStops[2].Offset = _3YellowStart + (fractionExplosionLifeTime * (_3YellowEnd - _3YellowStart));
            body.GradientBrush.GradientStops[3].Offset = _4OrangeStart + (fractionExplosionLifeTime * (_4OrangeEnd - _4OrangeStart));
        }

        //fade out the bomb blast
         static public void AnimateBombBodyGradientColor(Body body, double fractionExplosionLifeTime)
        {
            byte alpha = (byte)(255 - (255 * (fractionExplosionLifeTime)));
            foreach (BodyBrushGradientStop stop in body.GradientBrush.GradientStops)
            {
                if (stop.Color.A <= 0)  // don't change color that is already transparent
                    continue;

                stop.Color.A = alpha;   // this assuming all color band start from alpha=255
            }
        }

#endif

        /// <summary>
        /// returns a collection of bodies in the lit AABB around  a circle
        /// </summary>
        /// <param name="center">center in WCS</param>
        /// <param name="radius"></param>
        /// <returns></returns>
        static public ICollection<Body> GetBodiesInAABBofCirc(Vector2 center, Nullable< AABB> viewAABB, float radius, Spirit sp)
        {
            AABB aabb = new AABB(radius * 2f, radius * 2f, center - new Vector2(radius, radius));

            if ( viewAABB != null)
                aabb.Overlap((AABB)viewAABB);

            return sp.DetectBodiesInAABB(aabb);
        }


        static public ICollection<Body> GetBodiesInAABBofCirc(Vector2 center,  float radius, Spirit sp)
        {
            return GetBodiesInAABBofCirc(center, null , radius, sp);
        }



        static public void OnEndGasExplosion(Effect effect)
         {
             Body bombBody = effect.UserData as Body;
             World.Instance.RemoveBody(bombBody);
         }
    }

}

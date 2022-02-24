using Farseer.Xna.Framework;
using FarseerPhysics.Common;
using Microsoft.Xna.Framework;
using Vector2 = Farseer.Xna.Framework.Vector2;
using Vector3 = Farseer.Xna.Framework.Vector3;


namespace Core.Game.MG.Drawing
{
    /// <summary>
    /// A visible filled Polygon object
    /// </summary>
    public class PolygonView : ObjectView
    {

        public bool ShowFitSpline;

        public Vertices vertices;

        //this was vertics in world, pos is the pos of center i think
        //TODO need to add another for display likst, that was all the pts xformed to world
        //
        //.. take bodypts xformed cpy directly to that.   bottom line , we need a copy.
        //
        //this is a high level object for explosion view and the like.   this could have  have draw order..
        //
        //our dl object wil be stripped.. 


        public Vector2 Pos;
     
        //TODO brush lineweight stuff, seee myra, 

        public bool IsFilled = true;

        public PolygonView(Microsoft.Xna.Framework.Game game, Vertices verts) : base(game)
        {
            _sharedVertices = verts;
        
        }


		//NOTE see CardinalSpline file tested here generated the control Vertices.  need to test the 0.5 tightness thing, need to add UI to adjust each grip..

		//  see this: Numerics.Interpolation  CAN PROBABLY get the whole class from  somewhere, 

		//  and this http://glasnost.itcarlow.ie/~powerk/technology/xna/Catmulroml.html

		//https://stackoverflow.com/questions/14915849/xna-catmullrom-curves  java code 
		//https://community.monogame.net/t/monogame-splineflower-create-wonderful-smooth-bezier-catmulrom-and-hermite-splines/10912

		//https://github.com/sqrMin1/MonoGame.SplineFlower  TODO DOES CATMULLNOW!!!!
#if GRAPHICS_MG
        public class Catmul 
    {

        //Use GameObject in 3d space as your Vertices or define array with desired Vertices
        public Vertices Vertices;

        //Store Vertices on the Catmull curve so we can visualize them
        List<Vector2> newVertices = new List<Vector2>();

        //How many Vertices you want on the curve
        double amountOfVertices = 10.0;

        //set from 0-1
        public  double alpha = 0.5f;

        /////////////////////////////

        void Update()
        {
            CatmulRom();
        }

        void CatmulRom()
        {
            newVertices.Clear();

            Vector2 p0 = new Vector2(Vertices[0].X , Vertices[0].Y);
            Vector2 p1 = new Vector2(Vertices[1].X, Vertices[1].Y);
            Vector2 p2 = new Vector2(Vertices[2].X, Vertices[2].Y);
            Vector2 p3 = new Vector2(Vertices[3].X, Vertices[3].Y);

                double t0 = 0.0;
            double t1 = GetT(t0, p0, p1);
            double t2 = GetT(t1, p1, p2);
            double t3 = GetT(t2, p2, p3);

            for (double t = t1; t < t2; t += ((t2 - t1) / amountOfVertices))
            {

                    //do Vector2 extension that does a *
             //   Vector2 A1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
             //   Vector2 A2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
             //   Vector2  A3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;

             //   Vector2 B1 = (t2 - t) / (t2 - t0) * A1 + (t - t0) / (t2 - t0) * A2;
             //   Vector2 B2 = (t3 - t) / (t3 - t1) * A2 + (t - t1) / (t3 - t1) * A3;
//
             //   Vector2 C = (t2 - t) / (t2 - t1) * B1 + (t - t1) / (t2 - t1) * B2;

             //   newVertices.Add(C);
            }
        }
            /*

            void GetTrackPoints()
            {
                for (int num = 0; num < inputpoints.Count; num++)
                {
                    // Get the 4 required points for the catmull rom spline
                    Vector3 p1 = inputpoints[num - 1 < 0 ? inputpoints.Count - 1 : num - 1];
                    Vector3 p2 = inputpoints[num];
                    Vector3 p3 = inputpoints[(num + 1) % inputpoints.Count];
                    Vector3 p4 = inputpoints[(num + 2) % inputpoints.Count];

                    // Calculate number of iterations we use here based
                    // on the distance of the 2 points we generate new points from.
                    float distance = Vector3.Distance(p2, p3);
                    int numberOfIterations =
                        (int)(segmentsPer100Meters * (distance / 100.0f));
                    if (numberOfIterations <= 0)
                        numberOfIterations = 1;

                    for (int iter = 0; iter < numberOfIterations; iter++)
                    {
                        Vector3 newVertex = Vector3.CatmullRom(p1, p2, p3, p4, iter / (float)numberOfIterations);
                        points.Add(newVertex);
                    } // for (iter)
                } // for (num)
            }*/


            double GetT(double t, Vector2 p0, Vector2 p1)
        {
                double a = Math.Pow((p1.X - p0.X), 2.0f)   + Math.Pow( (p1.Y - p0.Y), 2.0);
            double b = Math.Pow(a, 0.5);
            double  c = Math.Pow(b, alpha);

            return (c + t);
        }

        //Visualize the Vertices
        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            foreach (Vector2 temp in newVertices)
            {
                Vector3 pos = new Vector3(temp.x, temp.y, 0);
                Gizmos.DrawSphere(pos, 0.3f);
            }
        }
    }





    package curves;

import java.awt.image.BufferedImage;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.RecursiveTask;

/**
 * Takes in a set of points and computes the B-spline for them by reducing the matrix multiplications.
 * The new spline points are returned
 * 
 * @author asevans
 * 
 */
public class BSpline {

	/**
	 * Our order which is the nth_degree+1
	 */
	private int order=4;

	/**
	 * Our control points as PointObject objects
	 */
	private ArrayList<PointObject> controlObjects;
	
	public BSpline() {

	}
	
	

	public int getOrder() {
		return order;
	}



	public void setOrder(int order) {
		this.order = order;
	}



	public ArrayList<PointObject> getControlObjects() {
		return controlObjects;
	}



	public void setControlObjects(ArrayList<PointObject> controlObjects) {
		this.controlObjects = controlObjects;
	}



	/**
	 * Take in four PointObject Objects as containing control points and calculate our curve.
	 * @param p1
	 * @param p2
	 * @param p3
	 * @param p4
	 * @return
	 */
	private PointObject calcPoint(PointObject p1, PointObject p2, PointObject p3, PointObject p4){

		PointObject retobject=new PointObject();
		
		float basisax=(float)(((-1*p1.getX())+(3*p2.getX())-(3*p3.getX())+p4.getX())/6.0);
		float basisay=(float)(((-1*p1.getY())+(3*p2.getY())-(3*p3.getY())+p4.getY())/6);
		
		float basisbx=(float)(((3*p1.getX())-(6*p2.getX())+(3*p3.getX()))/6);
		float basisby=(float)(((3*p1.getY())-(6*p2.getY())+(3*p3.getY()))/6);
		
		float basiscx=(float)(((-3*p1.getX())+(3*p3.getX()))/6);
		float basiscy=(float)(((-3*p1.getY())+(3*p3.getY()))/6);
		
		float basisdx=(float)((p1.getX()+(4*p2.getX())+p3.getX())/6);
		float basisdy=(float)((p2.getY()+(4*p2.getY())+p3.getY())/6);
		
		float t;
		
		//get the vector determinant
		for(int i=0;i<(order-1);i++){
			t=i/order;
			retobject.setX(((basiscx+t*(basisbx+t*basisax))*t+basisdx));
			retobject.setY(((basiscy+t*(basisby+t*basisay))*t+basisdy));
		
		}
		
		//return the point object
		return retobject;
	}
	
	
	/**
	 * Control the creation of bspline points.
	 * @return
	 */
	private ArrayList<PointObject> getSplinePoints(){
		
		ArrayList<PointObject> po=new ArrayList<PointObject>();
		
		if(controlObjects.size()>0){
			//calc bspline
			int i=0;
			
			//add the initial points
			while(i<3 & i<controlObjects.size()){
				po.add(controlObjects.get(i));
				i++;
			}
			
			//add the remaining calculated points
			if(controlObjects.size()>3){
				for(i=3;i<controlObjects.size();i++){
					po.add(calcPoint(controlObjects.get(i),controlObjects.get((i+1)),controlObjects.get((i+2)),controlObjects.get((i+3))));
				}
			}
		}
		else{
			try{
				throw new NullPointerException("No Points Provided!");
			}catch(NullPointerException e){
				e.printStackTrace();
			}
		}
		
		return po;
	}
	
	/**
	 * Run the program and get an ArrayList<PointObject> of spline points.
	 * The number of points returned depends on the number of control points provided.
	 * @return
	 */
	public ArrayList<PointObject> run(){
		return getSplinePoints();
	}
	
	



    
import java.util.ArrayList;

/**
 * Calculate the Bezier curve for a 2-dimensional object using the solution to the matrix equation.
 * @author asevans
 *
 */
public class Bezier {

	private int order=4;
	private ArrayList<PointObject> controlObjects;

	/**
	 * Empty Constructor
	 */
	public Bezier() {

	}

	/**
	 * Set the order (nth power +1)
	 * @return
	 */
	public int getOrder() {
		return order;
	}



	public void setOrder(int order) {
		this.order = order;
	}



	public ArrayList<PointObject> getControlObjects() {
		return controlObjects;
	}



	public void setControlObjects(ArrayList<PointObject> controlObjects) {
		this.controlObjects = controlObjects;
	}



	/**
	 * Get a spline point from four provided PointObject objects
	 * @param p1
	 * @param p2
	 * @param p3
	 * @param p4
	 * @return
	 */
	private PointObject calcPoint(PointObject p1, PointObject p2, PointObject p3, PointObject p4){

		//calc a point on the bezier curve
		PointObject retobject=new PointObject();
		
		//mutliply our matrix by the points which are not in vector form here (no direction needed then)
		float basisax=(float) p1.getX();
		float basisay=(float) p1.getY();
		
		float basisbx=(float)((-3*p1.getX())+(3*p2.getX()));
		float basisby=(float)((-3*p1.getY())+(3*p2.getY()));
		
		float basiscx=(float)((3*p1.getX())+(-6*p2.getX())+(3*p3.getX()));
		float basiscy=(float)((3*p1.getY())+(-6*p2.getY())+(3*p3.getY()));
		
		float basisdx=(float)((-1*p1.getX())+(3*p2.getX())-(3*p3.getX())+p4.getX());
		float basisdy=(float)((-1*p1.getY())+(3*p2.getY())-(3*p3.getY())+p4.getY());
		
		float t;
		
		//get the solution to our new matrix
		for(int i=0;i<order;i++){
			t=i/order;
			retobject.setX(basisax+t*(basisbx+t*(basiscx+(t*basisdx))));
			retobject.setY(basisay+t*(basisby+t*(basiscy+(t*basisdy))));
		}
		
		//return the calculated point object
		return retobject;
	}
	
	
	/**
	 * Control the calculation of the spline points
	 * @return
	 */
	private ArrayList<PointObject> calcCurve(){
		ArrayList<PointObject> po=new ArrayList<PointObject>();
		
		if(controlObjects.size()>0){
			//calc bezier curve
			int i=0;
			
			//add the initial points
			while(i<3 & i<controlObjects.size()){
				po.add(controlObjects.get(i));
				i++;
			}
			
			//add the remaining calculated points
			if(controlObjects.size()>3){
				for(i=3;i<controlObjects.size();i++){
					po.add(calcPoint(controlObjects.get(i),controlObjects.get((i+1)),controlObjects.get((i+2)),controlObjects.get((i+3))));
				}
			}
		}
		else{
			try{
				throw new NullPointerException("No Points Provided!");
			}catch(NullPointerException e){
				e.printStackTrace();
			}
		}
		
		return po;
	}
	
	/**
	 * Run the program and get an ArrayList<PointObject> of spline points.
	 * The number of points returned depends on the number of control points provided.
	 * @return
	 */
	public ArrayList<PointObject> getCurvePoints(){
		return calcCurve();
	}

}
}


 
ads via Carbon
Shortcut puts the "can" in Kanban and the agile in Agile. Delight the scrum gods and try us for free.
ADS VIA CARBON


 

#endif

		Vertices _sharedVertices;

        private void CreatePolygonContent(Vertices vertices, double thickness)
        {

            PolygonView polygon = ShapeFactory.CreatePolygonView(base.Game, vertices, thickness);
 

        }

        public void ModifyVertices(Vertices vertices)
        {

 


        }


    }

}

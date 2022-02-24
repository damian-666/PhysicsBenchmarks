using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace Core.Data.Geometry
{

    /// <summary>
    /// An interface to present a collection of  ControlPoint that can define a curve by interpolating a line or spline through, or some other function 
	/// of the ControlPoints parameters
    /// </summary>
    
	public interface IControlPoints
	{
		ControlPointCollection  ControlPoints {  get ; }
	};


    /// <summary>
    /// Used if the model points need to be transformed linearly to the control canvas.
    /// I think the canvas should be a view of the total world, since all coordinates are double.
    /// </summary>
   /*
    public interface IDrawable
	{
		Point[]  GetVertices();
	};
    */
}

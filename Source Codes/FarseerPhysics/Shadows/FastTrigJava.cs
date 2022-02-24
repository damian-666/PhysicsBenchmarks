#if FASTTRIG2
http://www.java-gaming.org/index.php?topic=14647.0

math.net numerics is better and cut out..

//Shared Code > 13.8x faster atan2(updated)   more confident its updated?

private static final int ATAN2_BITS = 7;

   private static final int ATAN2_BITS2 = ATAN2_BITS << 1;
   private static final int ATAN2_MASK = ~(-1 << ATAN2_BITS2);
   private static final int ATAN2_COUNT = ATAN2_MASK + 1;
   private static final int ATAN2_DIM = (int) Math.sqrt(ATAN2_COUNT);

   private static final float INV_ATAN2_DIM_MINUS_1 = 1.0f / (ATAN2_DIM - 1);
   private static final float DEG = 180.0f / (float) Math.PI;

   private static final float[] atan2 = new float[ATAN2_COUNT];


private static final int ATAN2_BITS = 7;

private static final int ATAN2_BITS2 = ATAN2_BITS << 1;
private static final int ATAN2_MASK = ~(-1 << ATAN2_BITS2);
private static final int ATAN2_COUNT = ATAN2_MASK + 1;
private static final int ATAN2_DIM = (int)Math.sqrt(ATAN2_COUNT);

private static final float INV_ATAN2_DIM_MINUS_1 = 1.0f / (ATAN2_DIM - 1);
private static final float DEG = 180.0f / (float)Math.PI;

private static final float[] atan2 = new float[ATAN2_COUNT];



static
   {
      for (int i = 0; i<ATAN2_DIM; i++)
      {
         for (int j = 0; j<ATAN2_DIM; j++)
         {
            float x0 = (float)i / ATAN2_DIM;
float y0 = (float)j / ATAN2_DIM;

atan2[j * ATAN2_DIM + i] = (float) Math.atan2(y0, x0);
         }
      }
   }


   /**
    * ATAN2
    */

   public static final float atan2Deg(float y, float x)
{
    return FastMath.atan2(y, x) * DEG;
}

public static final float atan2DegStrict(float y, float x)
{
    return (float)Math.atan2(y, x) * DEG;
}

public static final float atan2(float y, float x)
{
    float add, mul;

    if (x < 0.0f)
    {
        if (y < 0.0f)
        {
            x = -x;
            y = -y;

            mul = 1.0f;
        }
        else
        {
            x = -x;
            mul = -1.0f;
        }

        add = -3.141592653f;
    }
    else
    {
        if (y < 0.0f)
        {
            y = -y;
            mul = -1.0f;
        }
        else
        {
            mul = 1.0f;
        }

        add = 0.0f;
    }

    float invDiv = 1.0f / (((x < y) ? y : x) * INV_ATAN2_DIM_MINUS_1);

    int xi = (int)(x * invDiv);
    int yi = (int)(y * invDiv);

    return (atan2[yi * ATAN2_DIM + xi] + add) * mul;
}

//////

  
2  
3  
4  
5  
6  
7  
8  
9  
10  
11  
12  
13  
14  
15  
16  
17  
18  
19  
20  
21  
22  
23  
24  
25  
      float min = -100;
float max = +100;
float step = 0.12f;

      for (int i = 0; i< 8; i++)
      {
         long t0A = System.nanoTime() / 1000000L;
float sumA = 0.0f;
         for (float y = min; y<max; y += step)
            for (float x = min; x<max; x += step)
               sumA += FastMath.atan2(y, x);
         long t1A = System.nanoTime() / 1000000L;

long t0B = System.nanoTime() / 1000000L;
float sumB = 0.0f;
         for (float y = min; y<max; y += step)
            for (float x = min; x<max; x += step)
               sumB += Math.atan2(y, x);
         long t1B = System.nanoTime() / 1000000L;

System.out.println();
System.out.println("FastMath: " + (t1A - t0A) + "ms, sum=" + sumA);
         System.out.println("JavaMath: " + (t1B - t0B) + "ms, sum=" + sumB);
         System.out.println("factor: " + ((float) (t1B - t0B) / (t1A - t0A)));
      }



///

///from github  https://gist.github.com/volkansalma/2972237
# include <stdio.h>
# include <stdlib.h>
# include <math.h>

float atan2_approximation1(float y, float x);
float atan2_approximation2(float y, float x);

int main()
{
    float x = 1;
    float y = 0;


    for( y = 0; y < 2*M_PI; y+= 0.1 )
    {
        for(x = 0; x < 2*M_PI; x+= 0.1)
        {
            printf("atan2 for %f,%f: %f \n", y, x, atan2(y, x));
            printf("approx1 for %f,%f: %f \n", y, x, atan2_approximation1(y, x));
            printf("approx2 for %f,%f: %f \n \n", y, x, atan2_approximation2(y, x));
            getch();
        }
    }


    return 0;
}

float atan2_approximation1(float y, float x)
{
    //http://pubs.opengroup.org/onlinepubs/009695399/functions/atan2.html
    //Volkan SALMA

    const float ONEQTR_PI = M_PI / 4.0;
	const float THRQTR_PI = 3.0 * M_PI / 4.0;
	float r, angle;
	float abs_y = fabs(y) + 1e-10f;      // kludge to prevent 0/0 condition
	if ( x < 0.0f )
	{
		r = (x + abs_y) / (abs_y - x);
		angle = THRQTR_PI;
	}
	else
	{
		r = (x - abs_y) / (x + abs_y);
		angle = ONEQTR_PI;
	}
	angle += (0.1963f * r * r - 0.9817f) * r;
	if ( y < 0.0f )
		return( -angle );     // negate if in quad III or IV
	else
		return( angle );


}

#define PI_FLOAT     3.14159265f
#define PIBY2_FLOAT  1.5707963f
// |error| < 0.005
float atan2_approximation2( float y, float x )
{
	if ( x == 0.0f )
	{
		if ( y > 0.0f ) return PIBY2_FLOAT;
		if ( y == 0.0f ) return 0.0f;
		return -PIBY2_FLOAT;
	}
	float atan;
	float z = y/x;
	if ( fabs( z ) < 1.0f )
	{
		atan = z/(1.0f + 0.28f*z*z);
		if ( x < 0.0f )
		{
			if ( y < 0.0f ) return atan - PI_FLOAT;
			return atan + PI_FLOAT;
		}
	}
	else
	{
		atan = PIBY2_FLOAT - z/(z*z + 0.28f);
		if ( y < 0.0f ) return atan - PI_FLOAT;
	}
	return atan;
}






/////////////////

4. Arctangents

 

In a nutshell, the most useful application for the arctangent function is to convert between rectangular and polar coordinates. To be more specific, starting with the location (x, y), we want to find (theta, r) in polar coordinates. This calculation is usually performed as follows:

 

theta = ::atan2(y, x);

r = ::sqrt(x * x + y * y);

 

As with the sincos() case illustrated above, cycles are being thrown away here. The calculation of the arctangent automatically generates the square root as a by-product, so why not use it?

 

To this end, I’ve written the following methods:

 

real32 atan2r_(real32 y, real32 x, real32& r);

real64 atan2r_(real64 y, real64 x, real64& r);

 

which return the magnitude of the vector (x, y) in r, as well as the arctangent. Similarly to the sincos function, computing the atan can be reduced to the following steps:

1)    Normalize the vector (x, y), and reduce to the octant [0..π/4];

2)    Look up exact arcsin, cos values for some angle ‘phi’ near theta;

3)    Compute arcsin explicitly for ‘theta – phi’;

4)    Add the two arcsins to get arctan(theta);

5)    Expand back out to the interval [-π..π].

 

(1) Normalize the vector (x, y), and reduce to the octant [0..π/4]

 

This operation involves dividing (x, y) by the quantity sqrt(x^2 + y^2), which we can perform very efficiently (and without a floating-point divide!), using the sqrtinv_() method above. The octant reduction is done by direct comparison and branching, and ensures that 0 <= y <= x, which simplifies the remaining computation. The vector magnitude is also returned at this point.

 

(2) Look up exact arcsin, cos values for some angle ‘phi’ near theta

 

Since our vector (x, y) is now normalized, we know that y == sin(theta). So we can index into a table of values to find arcsin(phi), for some angle phi close to theta. Our index value gives us sin(phi), and we also look up cos(phi), which will aid us in our calculation.

 

(3) Compute arcsin explicitly for ‘theta – phi’

 

Since we already have (x, y) == sincos(theta), and we’ve looked up sincos(phi), we can multiply them together (actually, sincos(theta) * sincos(-phi)), to obtain sincos(theta – phi). From our new value sin(theta – phi), which is small, we can use the elegant Taylor expansion of the arcsin function:

 

 

to efficiently compute arcsin(theta – phi).

 

(4) Add the two arcsins to get arctan(theta)

 

This part seems almost magically simple, but mathematically it works out. (Trust me.)

 

(5) Expand back out to the interval [-π..π]

 

This step simply unwinds the reduction we performed in step (2), and yields the final arctangent value. Done!

 

To complete the suite of arctangent methods, it’s trivial to modify the following code to produce versions that return the inverse of the magnitude (useful for normalizing), no magnitude at all (simulating atan2), or atan(x) of one variable by observing that atan(x) == atan2(x, 1), on the interval [-π/2 .. π /2]. Here is the code, accurate to within 4 lsb’s in double precision:

 

static flag ataninited = false;

static real32 atanbuf_[257 * 2];

static real64 datanbuf_[513 * 2];

 

// ====================================================================

// arctan initialization

// =====================================================================

static void initatan_() {

if (ataninited)                   return;

uint32 ind;

for (ind = 0; ind <= 256; ind++) {

     real64 v = ind / 256.0;

     real64 asinv = ::asin(v);

     atanbuf_[ind * 2    ] = ::cos(asinv);

     atanbuf_[ind * 2 + 1] = asinv;

     }

for (ind = 0; ind <= 512; ind++) {

     real64 v = ind / 512.0;

     real64 asinv = ::asin(v);

     datanbuf_[ind * 2    ] = ::cos(asinv);

     datanbuf_[ind * 2 + 1] = asinv;

     }

ataninited = true;

}

 

// =====================================================================

// arctan, single-precision; returns theta and r

// =====================================================================

real32 atan2r_(real32 y_, real32 x_, real32& r_) {

Assert(ataninited);

real32 mag2 = x_ * x_ + y_ * y_;

if(!(mag2 > 0))  { goto zero; }   // degenerate case

real32 rinv = sqrtinv_(mag2);

uint32 flags = 0;

real32 x, y;

real32 ypbuf[1];

real32 yp = 32768;

if (y_ < 0 ) { flags |= 4; y_ = -y_; }

if (x_ < 0 ) { flags |= 2; x_ = -x_; }

if (y_ > x_) {

flags |= 1;

yp += x_ * rinv; x = rinv * y_; y = rinv * x_;

ypbuf[0] = yp;

}

else {

yp += y_ * rinv; x = rinv * x_; y = rinv * y_;

ypbuf[0] = yp;

}

r_ = rinv * mag2;

int32 ind = (((int32*)(ypbuf))[0] & 0x01FF) * 2 * sizeof(real32);

    

real32* asbuf = (real32*)(address(atanbuf_) + ind);

real32 sv = yp - 32768;

real32 cv = asbuf[0];

real32 asv = asbuf[1];

sv = y * cv - x * sv;    // delta sin value

// ____ compute arcsin directly

real32 asvd = 6 + sv * sv;   sv *= real32(1.0 / 6.0);

real32 th = asv + asvd * sv;

if (flags & 1) { th = _2pif / 4 - th; }

if (flags & 2) { th = _2pif / 2 - th; }

if (flags & 4) { return -th; }

return th;

zero:

r_ = 0; return 0;

}

 

// =====================================================================

// arctan, double-precision; returns theta and r

// =====================================================================

real64 atan2r_(real64 y_, real64 x_, real64& r_) {

Assert(ataninited);

const real32 _0 = 0.0;

real64 mag2 = x_ * x_ + y_ * y_;

if(!(mag2 > _0)) { goto zero; }   // degenerate case

real64 rinv = sqrtinv_(mag2);

uint32 flags = 0;

real64 x, y;

real64 ypbuf[1];

real64 _2p43 = 65536.0 * 65536.0 * 2048.0;

real64 yp = _2p43;

if (y_ < _0) { flags |= 4; y_ = -y_; }

if (x_ < _0) { flags |= 2; x_ = -x_; }

if (y_ > x_) {

flags |= 1;

yp += x_ * rinv; x = rinv * y_; y = rinv * x_;

ypbuf[0] = yp;

}

else {

yp += y_ * rinv; x = rinv * x_; y = rinv * y_;

ypbuf[0] = yp;

}

r_ = rinv * mag2;

int32 ind = (((int32*)(ypbuf))[iman_] & 0x03FF) * 16;

real64* dasbuf = (real64*)(address(datanbuf_) + ind);

real64 sv = yp - _2p43; // index fraction

real64 cv = dasbuf[0];

real64 asv = dasbuf[1];

sv = y * cv - x * sv;    // delta sin value

// ____ compute arcsin directly

real64 asvd = 6 + sv * sv;   sv *= real64(1.0 / 6.0);

real64 th = asv + asvd * sv;

if (flags & 1) { th = _2pi / 4 - th; }

if (flags & 2) { th = _2pi / 2 - th; }

if (flags & 4) { th = -th; }

return th;

zero:

r_ = _0; return _0;

}
#endif
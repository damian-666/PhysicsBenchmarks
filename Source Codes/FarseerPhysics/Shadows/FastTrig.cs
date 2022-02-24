
#if FASTTRIG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



math.net numerics is better and cut out..
but thry the cheapo ones first..

double FastArcTan(double x)   //TODO try for shadows  p1 shadow
{
    return M_PI_4 * x - x * (fabs(x) - 1) * (0.2447 + 0.0663 * fabs(x));
}















namespace FarseerPhysicsUA.Shadows
{
    class FastTrig
    {

//TODO p1  optimize MAth better use the general MST fast math..
        http://riven8192.blogspot.co.id/2009/08/fastmath-atan2-lookup-table.html

        //use LOOKUP  structurelinearlookup..
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

            float invDiv = ATAN2_DIM_MINUS_1 / ((x < y) ? y : x);

            int xi = (int)(x * invDiv);
            int yi = (int)(y * invDiv);

            return (atan2[yi * ATAN2_DIM + xi] + add) * mul;
        }


        private static final int ATAN2_BITS = 7;

        private static final int ATAN2_BITS2 = ATAN2_BITS << 1;
        private static final int ATAN2_MASK = ~(-1 << ATAN2_BITS2);
        private static final int ATAN2_COUNT = ATAN2_MASK + 1;
        private static final int ATAN2_DIM = (int)Math.sqrt(ATAN2_COUNT);

        private static final float ATAN2_DIM_MINUS_1 = (ATAN2_DIM - 1);

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




    }
}






//////

    int dim = 633 * 2; // random number times 2

float maxDiff = 0.0f;
float sumDiff = 0.0f;

      for (int i = 0; i<dim* dim; i++)
      {
         float x = (float)((i % dim) - (dim / 2)) / (dim / 2);
float y = (float)((i / dim) - (dim / 2)) / (dim / 2);
float slow = (float)Math.atan2(y, x);
float fast = FastMath.atan2(y, x);
float diff = Math.abs(slow - fast);
         if (diff > maxDiff)
            maxDiff = diff;
         sumDiff += diff;
      }

      float avgDiff = sumDiff / (dim * dim);

System.out.println("maxDiff=" + maxDiff); // 0.007858515
System.out.println("avgDiff=" + avgDiff); // 0.002910751






//update

http://www.java-gaming.org/index.php?topic=14647.0
    
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
26  
27  
28  
29  
30  
31  
32  
33  
34  
35  
36  
37  
38  
39  
40  
41  
42  
43  
44  
45  
46  
47  
48  
49  
50  
51  
52  
53  
54  
55  
56  
57  
58  
59  
60  
61  
62  
63  
64  
65  
66  
67  
68  
69  
70  
71  
72  
73  
74  
75  
76  
77  
78  
79  
80  
81  
82  
83  
84  
85  
86  
87  
88  
89  
90  
91  
92  
93  
94  
95  
96  
97  
98  
99  
100  
101  
102  
103  
104  
105  
public class FastMath
{
    public static void main(String[] args)
    {
        float min = -100;
        float max = +100;
        float step = 0.12f;

        for (int i = 0; i < 8; i++)
        {
            long t0A = System.nanoTime() / 1000000L;
            float sumA = 0.0f;
            for (float y = min; y < max; y += step)
                for (float x = min; x < max; x += step)
                    sumA += atan2(y, x);
            long t1A = System.nanoTime() / 1000000L;

            long t0B = System.nanoTime() / 1000000L;
            float sumB = 0.0f;
            for (float y = min; y < max; y += step)
                for (float x = min; x < max; x += step)
                    sumB += Math.atan2(y, x);
            long t1B = System.nanoTime() / 1000000L;

            System.out.println();
            System.out.println("FastMath: " + (t1A - t0A) + "ms, sum=" + sumA);
            System.out.println("JavaMath: " + (t1B - t0B) + "ms, sum=" + sumB);
            System.out.println("factor: " + (float)(t1B - t0B) / (t1A - t0A));
        }
    }

    private static final int SIZE = 1024;
    private static final float STRETCH = Math.PI;
    // Output will swing from -STRETCH to STRETCH (default: Math.PI)
    // Useful to change to 1 if you would normally do "atan2(y, x) / Math.PI"

    // Inverse of SIZE
    private static final int EZIS = -SIZE;
    private static final float[] ATAN2_TABLE_PPY = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_PPX = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_PNY = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_PNX = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_NPY = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_NPX = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_NNY = new float[SIZE + 1];
    private static final float[] ATAN2_TABLE_NNX = new float[SIZE + 1];

    static
    {
        for (int i = 0; i <= SIZE; i++)
        {
            float f = (float)i / SIZE;
    ATAN2_TABLE_PPY[i] = (float)(StrictMath.atan(f) * STRETCH / StrictMath.PI);
            ATAN2_TABLE_PPX[i] = STRETCH* 0.5f - ATAN2_TABLE_PPY[i];
            ATAN2_TABLE_PNY[i] = -ATAN2_TABLE_PPY[i];
            ATAN2_TABLE_PNX[i] = ATAN2_TABLE_PPY[i] - STRETCH* 0.5f;
            ATAN2_TABLE_NPY[i] = STRETCH - ATAN2_TABLE_PPY[i];
            ATAN2_TABLE_NPX[i] = ATAN2_TABLE_PPY[i] + STRETCH* 0.5f;
            ATAN2_TABLE_NNY[i] = ATAN2_TABLE_PPY[i] - STRETCH;
            ATAN2_TABLE_NNX[i] = -STRETCH* 0.5f - ATAN2_TABLE_PPY[i];
        }
    }

    /**
     * ATAN2
     */

    public static final float atan2(float y, float x)
{
    if (x >= 0)
    {
        if (y >= 0)
        {
            if (x >= y)
                return ATAN2_TABLE_PPY[(int)(SIZE * y / x + 0.5)];
            else
                return ATAN2_TABLE_PPX[(int)(SIZE * x / y + 0.5)];
        }
        else
        {
            if (x >= -y)
                return ATAN2_TABLE_PNY[(int)(EZIS * y / x + 0.5)];
            else
                return ATAN2_TABLE_PNX[(int)(EZIS * x / y + 0.5)];
        }
    }
    else
    {
        if (y >= 0)
        {
            if (-x >= y)
                return ATAN2_TABLE_NPY[(int)(EZIS * y / x + 0.5)];
            else
                return ATAN2_TABLE_NPX[(int)(EZIS * x / y + 0.5)];
        }
        else
        {
            if (x <= y) // (-x >= -y)
                return ATAN2_TABLE_NNY[(int)(SIZE * y / x + 0.5)];
            else
                return ATAN2_TABLE_NNX[(int)(SIZE * x / y + 0.5)];
        }
    }
}
}
#endif
﻿//******************************************************************************************************
//  CurveFit.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  01/24/2006 - J. Ritchie Carroll
//       Generated original version of source code.
//  09/17/2008 - J. Ritchie Carroll
//      Converted to C#.
//  08/07/2009 - Josh L. Patterson
//       Edited Comments.
//  09/14/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GSF.NumericalAnalysis
{
    /// <summary>
    /// Linear regression algorithm.
    /// </summary>
    public static class CurveFit
    {
        /// <summary>
        /// Computes linear regression over given values.
        /// </summary>
        /// <param name="polynomialOrder">An <see cref="int"/> for the polynomial order.</param>
        /// <param name="values">A list of values.</param>
        /// <returns>An array of <see cref="double"/> values.</returns>
        public static double[] Compute(int polynomialOrder, IEnumerable<Point> values)
        {
            return Compute(polynomialOrder, values.Select(point => point.X).ToList(), values.Select(point => point.Y).ToList());
        }

        /// <summary>
        /// Computes linear regression over given values.
        /// </summary>
        /// <param name="polynomialOrder">An <see cref="int"/> for the polynomial order.</param>
        /// <param name="xValues">A list of <see cref="double"/> x-values.</param>
        /// <param name="yValues">A list of <see cref="double"/> y-values.</param>
        /// <returns>An array of <see cref="double"/> values.</returns>
        public static double[] Compute(int polynomialOrder, IList<double> xValues, IList<double> yValues)
        {
            if ((object)xValues == null)
                throw new ArgumentNullException("xValues");

            if ((object)yValues == null)
                throw new ArgumentNullException("yValues");

            if (xValues.Count != yValues.Count)
                throw new ArgumentException("Point count for x-values and y-values must be equal");

            if (!(xValues.Count >= polynomialOrder + 1))
                throw new ArgumentException("Point count must be greater than requested polynomial order");

            if (!(polynomialOrder >= 1) && (polynomialOrder <= 7))
                throw new ArgumentOutOfRangeException("polynomialOrder", "Polynomial order must be between 1 and 7");

            // Curve fit function (courtesy of Brian Fox from DatAWare client code)
            double[] coeffs = new double[8];
            double[] sum = new double[22];
            double[] v = new double[12];
            double[,] b = new double[12, 13];
            double p, divB, fMultB, sigma;
            int ls, lb, lv, i1, i, j, k, l;
            int pointCount = xValues.Count;

            ls = polynomialOrder * 2;
            lb = polynomialOrder + 1;
            lv = polynomialOrder;
            sum[0] = pointCount;

            for (i = 0; i < pointCount; i++)
            {
                p = 1.0;
                v[0] = v[0] + yValues[i];

                for (j = 1; j <= lv; j++)
                {
                    p = xValues[i] * p;
                    sum[j] = sum[j] + p;
                    v[j] = v[j] + yValues[i] * p;
                }

                for (j = lb; j <= ls; j++)
                {
                    p = xValues[i] * p;
                    sum[j] = sum[j] + p;
                }
            }

            for (i = 0; i <= lv; i++)
            {
                for (k = 0; k <= lv; k++)
                {
                    b[k, i] = sum[k + i];
                }
            }

            for (k = 0; k <= lv; k++)
            {
                b[k, lb] = v[k];
            }

            for (l = 0; l <= lv; l++)
            {
                divB = b[0, 0];
                for (j = l; j <= lb; j++)
                {
                    if (divB == 0)
                        divB = 1;
                    b[l, j] = b[l, j] / divB;
                }

                i1 = l + 1;

                if (i1 - lb < 0)
                {
                    for (i = i1; i <= lv; i++)
                    {
                        fMultB = b[i, l];
                        for (j = l; j <= lb; j++)
                        {
                            b[i, j] = b[i, j] - b[l, j] * fMultB;
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            coeffs[lv] = b[lv, lb];
            i = lv;

            do
            {
                sigma = 0;
                for (j = i; j <= lv; j++)
                {
                    sigma = sigma + b[i - 1, j] * coeffs[j];
                }
                i--;
                coeffs[i] = b[i, lb] - sigma;
            }
            while (i - 1 > 0);

            #region [ Old Code ]

            //    For i = 1 To 7
            //        Debug.Print "Coeffs(" & i & ") = " & Coeffs(i)
            //    Next i

            //For i = 1 To 60
            //    '        CalcY(i).TTag = xValues(1) + ((i - 1) / (xValues(pointCount) - xValues(1)))

            //    CalcY(i).TTag = ((i - 1) / 59) * xValues(pointCount) - xValues(1)
            //    CalcY(i).Value = Coeffs(1)

            //    For j = 1 To polynomialOrder
            //        CalcY(i).Value = CalcY(i).Value + Coeffs(j + 1) * CalcY(i).TTag ^ j
            //    Next
            //Next

            //    SSERROR = 0
            //    For i = 1 To pointCount
            //        SSERROR = SSERROR + (yValues(i) - CalcY(i).Value) * (yValues(i) - CalcY(i).Value)
            //    Next i
            //    SSERROR = SSERROR / (pointCount - polynomialOrder)
            //    sError = SSERROR

            #endregion

            // Return slopes...
            return coeffs;
        }

        /// <summary>
        /// Uses least squares linear regression to estimate the coefficients a, b, and c
        /// from the given (x,y,z) data points for the equation z = a + bx + cy.
        /// </summary>
        /// <param name="zValues">z-value array</param>
        /// <param name="xValues">x-value array</param>
        /// <param name="yValues">y-value array</param>
        /// <param name="a">the out a coefficient</param>
        /// <param name="b">the out b coefficient</param>
        /// <param name="c">the out c coefficient</param>
        public static void LeastSquares(double[] zValues, double[] xValues, double[] yValues, out double a, out double b, out double c)
        {
            double n = zValues.Length;

            double xSum = 0;
            double ySum = 0;
            double zSum = 0;

            double xySum = 0;
            double xzSum = 0;
            double yzSum = 0;

            double xxSum = 0;
            double yySum = 0;

            double[,] coeff = new double[3, 4];

            for (int i = 0; i < n; i++)
            {
                double x = xValues[i];
                double y = yValues[i];
                double z = zValues[i];

                xSum += x;
                ySum += y;
                zSum += z;

                xySum += x * y;
                xzSum += x * z;
                yzSum += y * z;

                xxSum += x * x;
                yySum += y * y;
            }

            coeff[0, 0] = zSum;
            coeff[0, 1] = n;
            coeff[0, 2] = xSum;
            coeff[0, 3] = ySum;

            coeff[1, 0] = xzSum - (xSum * zSum) / n;
            coeff[1, 1] = 0;
            coeff[1, 2] = xxSum - (xSum * xSum) / n;
            coeff[1, 3] = xySum - (xSum * ySum) / n;

            coeff[2, 0] = yzSum - (ySum * zSum) / n - (coeff[1, 0] * coeff[1, 3]) / coeff[1, 2];
            coeff[2, 1] = 0;
            coeff[2, 2] = 0;
            coeff[2, 3] = yySum - (ySum * ySum) / n - (coeff[1, 3] * coeff[1, 3]) / coeff[1, 2];

            c = coeff[2, 0] / coeff[2, 3];
            b = (coeff[1, 0] - c * coeff[1, 3]) / coeff[1, 2];
            a = (coeff[0, 0] - b * coeff[0, 2] - c * coeff[0, 3]) / coeff[0, 1];
        }
    }
}
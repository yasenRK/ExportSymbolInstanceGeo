﻿using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;

namespace ExportSymbolInstanceGeo
{
  class Util
  {
    #region Formatting
    /// <summary>
    /// Return a string for a real number
    /// formatted to four decimal places.
    /// </summary> 
    public static string RealString( double a )
    {
      return a.ToString( "0.####" );
    }

    /// <summary>
    /// Return a string for an XYZ point
    /// or vector with its coordinates
    /// formatted to four decimal places.
    /// </summary>
    public static string PointString(
      XYZ p,
      bool onlySpaceSeparator = false,
      bool enclosingParenthesis = true,
      bool enclosingBrackets = false )
    {
      string format_string = onlySpaceSeparator
        ? "{0} {1} {2}"
        : "{0},{1},{2}";

      if( enclosingParenthesis )
      {
        format_string = "(" + format_string + ")";
      }

      if( enclosingBrackets )
      {
        format_string = "[" + format_string + "]";
      }

      return string.Format( format_string,
        RealString( p.X ),
        RealString( p.Y ),
        RealString( p.Z ) );
    }

    /// <summary>
    /// Append the given lines to a JSON file
    /// </summary>
    public static void WriteJsonFile( 
      string path,
      IList<string> lines )
    {
      using( StreamWriter s = new StreamWriter(
        path, true ) )
      {
        string a = "{\r\n"
          + string.Join( ",\r\n", lines )
          + "\r\n}\r\n";

        s.Write( a );
        s.Close();
      }
    }
    #endregion // Formatting

    #region Unit Handling
    const double _inchToMm = 25.4;
    const double _footToMm = 12 * _inchToMm;
    const double _footToMeter = _footToMm * 0.001;
    const double _sqfToSqm = _footToMeter * _footToMeter;
    const double _cubicFootToCubicMeter = _footToMeter * _sqfToSqm;

    /// <summary>
    /// Convert a given length in feet to millimetres.
    /// </summary>
    public static double FootToMm( double length )
    {
      return length * _footToMm;
    }

    /// <summary>
    /// Convert a given length in feet to millimetres,
    /// rounded to the closest millimetre.
    /// </summary>
    public static int FootToMmInt( double length )
    {
      //return (int) ( _feet_to_mm * d + 0.5 );
      return (int) Math.Round( _footToMm * length,
        MidpointRounding.AwayFromZero );
    }
    #endregion // Unit Handling
  }
}

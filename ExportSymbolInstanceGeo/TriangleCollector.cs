﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;

namespace ExportSymbolInstanceGeo
{
  /// <summary>
  /// Collect triangles from the solids of a given element.
  /// Return them as element instance data in the project.
  /// If the element contains one single level of symbol 
  /// geometry, return that also, toether with the required
  /// transformation from symbol to project coordinates.
  /// </summary>
  class TriangleCollector
  {
    #region Triangle indices
    class TriangleIndices
    {
      public int A { get; set; }
      public int B { get; set; }
      public int C { get; set; }
      public TriangleIndices( int a, int b, int c )
      {
        A = a;
        B = b;
        C = c;
      }
      public override string ToString()
      {
        return string.Format( "{0} {1} {2}", A, B, C );
      }
    }
    #endregion // Triangle indices

    #region JtFace
    class JtFace : List<TriangleIndices>
    {
      public JtFace(int capacity) : base( capacity ) { }
    }
    #endregion // JtFace

    #region JtSolid
    class JtSolid : List<JtFace>
    {
      public JtSolid( int capacity ) : base( capacity ) { }
    }
    #endregion // JtSolid

    bool _uses_instance_geometry;
    IntVertexLookup _vertices;
    List<JtSolid> _instance_solids;
    List<JtFace> _instance_meshes;
    List<JtSolid> _symbol_solids;
    List<JtFace> _symbol_meshes;
    Transform _symbol_transform;
    List<Transform> _transformations;
    int _max_nesting_level;

    #region Transform stack
    bool InSymbol
    {
      get
      {
        return null != _transformations;
      }
    }

    void PushTransformation( Transform t )
    {
      //Debug.Assert( null == _transformations,
      //  "currently only one level deep supported" );

      if( null == _transformations )
      {
        _transformations = new List<Transform>( 1 );
      }
      _transformations.Add( t );
      ++_max_nesting_level;
    }

    void PopTransformation()
    {
      //Debug.Assert( null != _transformations,
      //  "cannot pop transform from empty stack" );

      int n = _transformations.Count;

      if( 1 == n )
      {
        _transformations = null;
      }
      else
      {
        _transformations.RemoveAt( n - 1 );
      }
    }

    XYZ TransformPoint( XYZ p )
    {
      XYZ pt = p;
      if( null != _transformations )
      {
        int n = _transformations.Count;
        for( int i = n - 1; i >= 0; --i )
        {
          pt = _transformations[ i ].OfPoint( pt );
        }
      }
      return pt;
    }
    #endregion // Transform stack

    #region Get triangle vertices
    int VertexIndexOf( XYZ p )
    {
      return _vertices.Add( new IntPoint3d( p ) );
    }

    TriangleIndices GetInstanceTriangle( XYZ p, XYZ q, XYZ r )
    {
      return new TriangleIndices(
        VertexIndexOf( TransformPoint( p ) ),
        VertexIndexOf( TransformPoint( q ) ),
        VertexIndexOf( TransformPoint( r ) ) );
    }

    TriangleIndices GetSymbolTriangle( XYZ p, XYZ q, XYZ r )
    {
      TriangleIndices ti = null;

      if( InSymbol )
      {
        Debug.Assert( 1 == _transformations.Count,
          "expected single level of symbol transformations" );

        if( null == _symbol_transform )
        {
          _symbol_transform = _transformations[ 0 ];
        }
        else
        {
          Debug.Assert( _symbol_transform.AlmostEqual(
            _transformations[ 0 ] ) );
        }

        ti = new TriangleIndices(
          VertexIndexOf( p ),
          VertexIndexOf( q ),
          VertexIndexOf( r ) );
      }
      return ti;
    }
    #endregion // Store vertices, lines and triangles

    #region Private helper methods
    /// <summary>
    /// Get geometry triangles from an element
    /// </summary>
    void DrawElement( Element e )
    {
      // If it is a Group, look at its components

      if( e is Group )
      {
        IList<ElementId> ids = (e as Group).GetMemberIds();
        foreach( ElementId id in ids )
        {
          DrawElement( e.Document.GetElement( id ) );
        }
      }

      Options opt = new Options();
      GeometryElement geo = e.get_Geometry( opt );
      DrawGeometry( geo );
    }

    /// <summary>
    /// Get geometry triangles from a geometry element
    /// </summary>
    void DrawGeometry( GeometryElement geo )
    {
      if( null == geo )
      {
        return;
      }
      foreach( GeometryObject obj in geo )
      {
        if( obj is Curve )
        {
          //DrawCurve( obj as Curve );
        }
        else if( obj is GeometryInstance )
        {
          DrawInstance( obj as GeometryInstance );
        }
        else if( obj is Mesh )
        {
          JtFace fInstance;
          JtFace fSymbol;
          GetMesh( obj as Mesh, out fInstance, out fSymbol );
          _instance_meshes.Add( fInstance );
          if(InSymbol)
          {
            _symbol_meshes.Add( fSymbol );
          }
        }
        else if( obj is PolyLine )
        {
          //DrawPolyLine( obj as PolyLine );
        }
        else if( obj is Solid )
        {
          JtSolid sInstance;
          JtSolid sSymbol;
          GetSolid( obj as Solid, out sInstance, out sSymbol );
          _instance_solids.Add( sInstance );
          if( InSymbol )
          {
            _symbol_solids.Add( sSymbol );
          }
        }
      }
    }

    void DrawInstance( GeometryInstance inst )
    {
      GeometryElement symbol_geo = inst.SymbolGeometry;

      if( null != symbol_geo )
      {
        PushTransformation( inst.Transform );
        DrawGeometry( symbol_geo );
        PopTransformation();
      }
    }

    void GetMesh( 
      Mesh mesh, 
      out JtFace fInstance,
      out JtFace fSymbol )
    {
      int n = mesh.NumTriangles;
      fInstance = new JtFace( n );
      fSymbol = InSymbol ? new JtFace( n ) : null;
      for( int i = 0; i < n; ++i )
      {
        MeshTriangle t = mesh.get_Triangle( i );
        XYZ p = t.get_Vertex( 0 );
        XYZ q = t.get_Vertex( 1 );
        XYZ r = t.get_Vertex( 2 );
        fInstance.Add( GetInstanceTriangle( p, q, r ) );
        if( InSymbol )
        {
          fSymbol.Add( GetSymbolTriangle( p, q, r ) );
        }
      }
    }

    void GetSolid( 
      Solid solid,
      out JtSolid sInstance,
      out JtSolid sSymbol )
    {
      int n = solid.Faces.Size;
      sInstance = new JtSolid( n );
      sSymbol = InSymbol ? new JtSolid( n ) : null;
      JtFace fInstance;
      JtFace fSymbol;
      foreach( Face f in solid.Faces )
      {
        GetMesh( f.Triangulate(), out fInstance, out fSymbol );
        sInstance.Add( fInstance );
        if( InSymbol )
        {
          sSymbol.Add( fSymbol );
        }
      }
    }
    #endregion // Private helper methods

    /// <summary>
    /// Collect all triangles from the solid
    /// of the given element.
    /// </summary>
    public TriangleCollector( Element e )
    {
      FamilyInstance fi = e as FamilyInstance;
      _uses_instance_geometry = (null == fi)
        ? false 
        : Autodesk.Revit.DB.IFC.ExporterIFCUtils
          .UsesInstanceGeometry( fi );

      _vertices = new IntVertexLookup();
      _instance_solids = new List<JtSolid>();
      _instance_meshes = new List<JtFace>();
      _symbol_solids = new List<JtSolid>();
      _symbol_meshes = new List<JtFace>();
      _symbol_transform = null;
      _transformations = null;
      _max_nesting_level = 0;

      DrawElement( e );

      Debug.Assert( (0 == _max_nesting_level) || (null == fi), 
        "expected zero symbol nesting for non-family-instance" );
    }

    #region Public output data accessors
    public string VertexCoordinates
    {
      get
      {
        return _vertices.Coordinates;
      }
    }

    public bool HasSymbol
    {
      get
      {
        return null != _symbol_transform;
      }
    }

    public bool IsNested
    {
      get
      {
        return 1 < _max_nesting_level;
      }
    }

    public string SymbolTriangleIndices
    {
      get
      {
        return string.Join( " ",
          _symbol_triangles.Select(
            t => t.ToString() ) );
      }
    }

    public string SymbolRotation
    {
      get
      {
        Transform t = _symbol_transform;
        return Util.PointString( t.BasisX, true )
          + " " + Util.PointString( t.BasisY, true )
          + " " + Util.PointString( t.BasisZ, true );
      }
    }

    public string SymbolTranslation
    {
      get
      { 
        IntPoint3d origin = new IntPoint3d( 
          _symbol_transform.Origin );

        return origin.ToString( true );
      }
    }
    #endregion // Public output data accessors
  }
}

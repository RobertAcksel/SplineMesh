using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace SplineMesh {
/// <summary>
/// A component that create a deformed mesh from a given one, according to a cubic Bézier curve and other parameters.
/// The mesh will always be bended along the BendAxis. Extreme BendAxis coordinates of source mesh verticies will be used as a bounding to the deformed mesh.
/// The resulting mesh is stored in a MeshFilter component and automaticaly updated each time the cubic Bézier curve control points are changed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public class MeshBender : MonoBehaviour {
	private Mesh source;
	[SerializeField, HideInInspector]
	private SerializeableMesh generatedMesh;
	private readonly List<Vertex> vertices = new List<Vertex>();

	private Quaternion sourceRotation;
	private Vector3 sourceTranslation;

	public ICurve curve;
	private float startScale = 1;
	private float endScale = 1;
	private float startRoll;
	private float endRoll;

	public enum Axis {
		X,
		Z
	}

	public Axis BendAxis = Axis.Z;

	private void OnEnable() {
		GetComponent<MeshFilter>().sharedMesh = generatedMesh;
	}

	/// <summary>
	/// Set the cubic Bézier curve to use to bend the source mesh, and begin to listen to curve control points for changes.
	/// </summary>
	/// <param name="curve"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetCurve(ICurve curve, bool update = true) {
		if(this.curve != null) {
			this.curve.Changed.RemoveListener(() => Compute());
		}

		this.curve = curve;
		curve.Changed.AddListener(() => Compute());
		if(update) Compute();
	}

	/// <summary>
	/// Set the scale of the mesh at curve start.
	/// </summary>
	/// <param name="scale"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetStartScale(float scale, bool update = true) {
		this.startScale = scale;
		if(update) Compute();
	}

	/// <summary>
	/// Set the scale of the mesh at curve end. If scale is different between start and end, the value will be interpolated along the curve.
	/// </summary>
	/// <param name="scale"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetEndScale(float scale, bool update = true) {
		this.endScale = scale;
		if(update) Compute();
	}

	/// <summary>
	/// Set the roll of the mesh (rotation around BendAxis) at curve start.
	/// </summary>
	/// <param name="scale"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetStartRoll(float roll, bool update = true) {
		this.startRoll = roll;
		if(update) Compute();
	}

	/// <summary>
	/// Set the roll of the mesh (rotation around BendAxis) at curve end. If roll is different between start and end, the value will be interpolated along the curve.
	/// </summary>
	/// <param name="scale"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetEndRoll(float roll, bool update = true) {
		this.endRoll = roll;
		if(update) Compute();
	}

	/// <summary>
	/// Set the source mesh.
	/// </summary>
	/// <param name="mesh"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetSourceMesh(Mesh mesh, bool update = true) {
		if(source != mesh) {
			this.source = mesh;
			vertices.Clear();
			int i = 0;
			foreach(Vector3 vert in source.vertices) {
				Vertex v = new Vertex();
				v.v = vert;
				v.n = source.normals[i++];
				vertices.Add(v);
			}
		}

		if(update) Compute();
	}

	/// <summary>
	/// Set the rotation to apply to the source mesh before anything happens. Because source mesh will always be bended along the X axis but may be oriented differently.
	/// </summary>
	/// <param name="rotation"></param>
	/// <param name="update">If let to true, update the resulting mesh immediatly.</param>
	public void SetRotation(Quaternion rotation, bool update = true) {
		this.sourceRotation = rotation;
		if(update) Compute();
	}

	/// <summary>
	/// Set an offset to bend the mesh outside the spline.
	/// </summary>
	/// <param name="translation"></param>
	/// <param name="update"></param>
	public void SetTranslation(Vector3 translation, bool update = true) {
		sourceTranslation = translation;
		if(update) Compute();
	}

	private void Compute() {
		if(source == null)
			return;

		if(generatedMesh == null) {
			generatedMesh = new Mesh();
		}

		int nbVert = source.vertices.Length;
		// find the bounds along BendAxis
		float minX = float.MaxValue;
		float maxX = float.MinValue;
		foreach(Vertex vert in vertices) {
			Vector3 p = vert.v;
			if(sourceRotation != Quaternion.identity) {
				p = sourceRotation * p;
			}

			if(sourceTranslation != Vector3.zero) {
				p += sourceTranslation;
			}

			switch(BendAxis) {
				case Axis.X:
					maxX = Math.Max(maxX, p.x);
					minX = Math.Min(minX, p.x);
					break;
				case Axis.Z:
					maxX = Math.Max(maxX, p.z);
					minX = Math.Min(minX, p.z);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		float length = Math.Abs(maxX - minX);

		List<Vector3> deformedVerts = new List<Vector3>(nbVert);
		List<Vector3> deformedNormals = new List<Vector3>(nbVert);
		// for each mesh vertex, we have to find its projection on the curve
		foreach(Vertex vert in vertices) {
			Vector3 p = vert.v;
			Vector3 n = vert.n;
			//  application of rotation
			if(sourceRotation != Quaternion.identity) {
				p = sourceRotation * p;
				n = sourceRotation * n;
			}

			if(sourceTranslation != Vector3.zero) {
				p += sourceTranslation;
			}

			float distanceRate;

			switch(BendAxis) {
				case Axis.X:
					distanceRate = Math.Abs(p.x - minX) / length;
					break;
				case Axis.Z:
					distanceRate = Math.Abs(p.z - minX) / length;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			Vector3 curvePoint = curve.GetLocationAtDistance(curve.Length * distanceRate);
			Vector3 curveTangent = curve.GetTangentAtDistance(curve.Length * distanceRate);
			Quaternion q = CubicBezierCurve.GetRotationFromTangent(curveTangent);

			// application of scale
			float scaleAtDistance = startScale + (endScale - startScale) * distanceRate;
			p *= scaleAtDistance;

			// application of roll
			float rollAtDistance = startRoll + (endRoll - startRoll) * distanceRate;

			switch(BendAxis) {
				case Axis.X:
					p = Quaternion.AngleAxis(rollAtDistance, Vector3.right) * p;
					n = Quaternion.AngleAxis(rollAtDistance, Vector3.right) * n;
					// reset X value of p
					p = new Vector3(0, p.y, p.z);
					break;
				case Axis.Z:
					p = Quaternion.AngleAxis(rollAtDistance, Vector3.forward) * p;
					n = Quaternion.AngleAxis(rollAtDistance, Vector3.forward) * n;
					// reset Z value of p
					p = new Vector3(p.x, p.y, 0);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			Vector3 fvert = q * p + curvePoint;
			deformedVerts.Add(fvert);
			deformedNormals.Add(q * n);
		}

		generatedMesh.vertices = deformedVerts.ToArray();
		generatedMesh.normals = deformedNormals.ToArray();
		generatedMesh.uv = source.uv;
		generatedMesh.triangles = source.triangles;
		GetComponent<MeshFilter>().mesh = generatedMesh;
	}

	private struct Vertex {
		public Vector3 v;
		public Vector3 n;
	}

	private void OnDestroy() {
		curve?.Changed.RemoveListener(() => Compute());
	}
}
}

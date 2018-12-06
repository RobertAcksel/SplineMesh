using System;
using UnityEngine;

namespace SplineMesh {
[Serializable]
public class SerializeableMesh {
	[SerializeField]
	public Vector3[] vertices;
	[SerializeField]
	public Vector3[] normals;
	[SerializeField]
	public Vector2[] uv;
	[SerializeField]
	public int[] triangles;

	private SerializeableMesh(Mesh sm) : this(sm.vertices, sm.normals, sm.uv, sm.triangles) { }

	public SerializeableMesh(Vector3[] vertices, Vector3[] normals, Vector2[] uv, int[] triangles) {
		this.vertices = vertices;
		this.normals = normals;
		this.uv = uv;
		this.triangles = triangles;
	}

	public static implicit operator SerializeableMesh(Mesh sm) {
		return new SerializeableMesh(sm);
	}

	public static implicit operator Mesh(SerializeableMesh sm) {
		return sm?.BuildMesh();
	}

	private Mesh BuildMesh() {
		var m = new Mesh();
		m.vertices = this.vertices;
		m.normals = this.normals;
		m.uv = this.uv;
		m.triangles = this.triangles;
		m.RecalculateBounds();
		m.RecalculateTangents();
		return m;
	}
}
}

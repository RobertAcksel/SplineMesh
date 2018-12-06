using System;
using System.Collections.Generic;
using System.Linq;
using SplineMesh;
using UnityEngine;
using UnityEngine.Events;

namespace DigitalLab.SplineMesh {
[ExecuteInEditMode]
[SelectionBase]
public class SplinePrefabDistribution : MonoBehaviour {
	public GameObject prefab = null;
	public float scale = 1, scaleRange = 0;
	public float spacing = 1, spacingRange = 0;
	public Vector3 offset;
	public float offsetRange = 0;
	public bool isRandomYaw = false;
	public int randomSeed = 0;

	[HideInInspector]
	public List<GameObject> spawnedGos = new List<GameObject>();

	private Spline spline = null;
	public bool ToUpdate = true;
	[SerializeField]
	private Data SpawnData;
//	[SerializeField]
	private bool UniformMeshDistribution = true;

	[Serializable]
	private class Data {
		[SerializeField]
		public List<FilterData> FilterData = new List<FilterData>();
		[SerializeField]
		public GameObject Prefab;
	}

	[Serializable]
	private class FilterData {
		[SerializeField]
		public MeshFilter Filter;
		[SerializeField]
		public bool Bend = true;
		[SerializeField]
		public Vector3 Offset;
		[SerializeField]
		public bool Spawn = true;

		public FilterData(MeshFilter filter) {
			this.Filter = filter;
		}
	}

	private FilterData GetSpawnDataForFilter(MeshFilter filter) {
		return SpawnData.FilterData.FirstOrDefault(fd => fd.Filter.name == filter.name); //use a different identification mechanic
	}

#if UNITY_EDITOR
	private void OnEnable() {
		if (!Application.isPlaying) {
			spline = GetComponent<Spline>();
			spline.NodeCountChanged.AddListener(() => {
				foreach (CubicBezierCurve curve in spline.GetCurves()) {
					curve.Changed.AddListener(() => ToUpdate = true);
				}
			});
			foreach (CubicBezierCurve curve in spline.GetCurves()) {
				curve.Changed.AddListener(() => ToUpdate = true);
			}
		}
	}
#endif

	private void AnalyzePrefab() {
		if (!prefab) {
			return;
		}

		var filters = prefab.GetComponentsInChildren<MeshFilter>();
		var d = SpawnData ?? new Data {Prefab = prefab};
		d.Prefab = prefab;

		foreach (var filter in filters) {
			if (d.FilterData.Any(e => e.Filter == filter)) {
				continue;
			}
			d.FilterData.Add(new FilterData(filter));
		}

		d.FilterData.RemoveAll(e => filters.All(f => f != e.Filter));

		SpawnData = d;
	}

#if UNITY_EDITOR
	[ContextMenu(nameof(ClearChildren))]
	public void ClearChildren() {
		int childCount = transform.childCount;
		for (int i = 0; i < childCount; i++) {
			GameObject.DestroyImmediate(transform.GetChild(0).gameObject);
		}
	}

	[ContextMenu(nameof(RebuildTrack))]
	public void RebuildTrack() {
		ToUpdate = true;
	}

	private void OnValidate() {
		AnalyzePrefab();
	}

	private void Update() {
		if (!Application.isPlaying) {
			if (ToUpdate) {
				Sow();
				ToUpdate = false;
			}
		}
	}
#endif

	private void OnDestroy() {
		Clear();
	}

	public void Sow() {
		Clear();

		UnityEngine.Random.InitState(randomSeed);
		if (spacing + spacingRange <= 0 ||
		    prefab == null)
			return;

		float distance = 0;
		while (distance <= spline.Length) {
			GameObject go = Instantiate(prefab, transform);
			go.transform.localRotation = Quaternion.identity;
			go.transform.localPosition = Vector3.zero;
			go.transform.localScale = Vector3.one;
			go.hideFlags = HideFlags.NotEditable;
#if UNITY_EDITOR
			foreach (Transform t in go.transform) {
				t.hideFlags = HideFlags.NotEditable;
			}
#endif

			// move along spline, according to spacing + random
			go.transform.localPosition = spline.GetLocationAlongSplineAtDistance(distance);
			// apply scale + random
			float rangedScale = scale + UnityEngine.Random.Range(0, scaleRange);
			go.transform.localScale = new Vector3(rangedScale, rangedScale, rangedScale);
			// rotate with random yaw
			if (isRandomYaw) {
				go.transform.Rotate(0, 0, UnityEngine.Random.Range(-180, 180));
			} else {
				Vector3 horTangent = spline.GetTangentAlongSplineAtDistance(distance);
				go.transform.rotation = Quaternion.LookRotation(horTangent) * Quaternion.LookRotation(Vector3.forward, Vector3.up);
			}

			var nextDistance = distance + spacing + UnityEngine.Random.Range(0, spacingRange);

			var filters = go.GetComponentsInChildren<MeshFilter>();

			// move orthogonaly to the spline, according to offset + random
			Vector3 binormal = spline.GetTangentAlongSplineAtDistance(distance);
			binormal = Quaternion.LookRotation(Vector3.forward, Vector3.up) * binormal;
//			binormal *= offset.z + UnityEngine.Random.Range(0, offsetRange * Math.Sign(offset.z));
			go.transform.position += binormal + new Vector3(offset.x, offset.y, offset.z);

			spawnedGos.Add(go);

			ICurve curve;
			if (UniformMeshDistribution) {
				curve = new SegmentedCurve(distance, nextDistance, spline);
			} else {
				curve = spline.GetCurveAtDistance(distance);
			}

			foreach (var filter in filters) {
				var spd = GetSpawnDataForFilter(filter);

				filter.gameObject.SetActive(spd.Spawn);

				filter.transform.localPosition += spd.Offset;

				if (!spd.Bend) {
					continue;
				}

				var mesh = filter.sharedMesh;
				var mb = filter.gameObject.AddComponent<MeshBender>();
				mb.SetSourceMesh(mesh, false);

				//this fixed the mesh transposition and rotation problem. its more a workaround than a fix but for now but still does the job
				mb.transform.position -= go.transform.localPosition;
				mb.transform.rotation *= Quaternion.Inverse(go.transform.rotation);

				mb.SetCurve(curve, false);
				mb.SetStartScale(scale, false);
				mb.SetEndScale(scale);
			}

			distance = nextDistance;
		}
	}

	private class SegmentedCurve : ICurve {
		private readonly float startLength;
		private readonly float endLength;
		private readonly ICurve sourceCurve;

		public SegmentedCurve(float startLength, float endLength, ICurve sourceCurve) {
			this.startLength = startLength;
			this.endLength = endLength;
			this.sourceCurve = sourceCurve;
		}

		public Vector3 GetLocationAtTime(float t) {
			throw new NotImplementedException();
		}

		public Vector3 GetTangentAtTime(float t) {
			throw new NotImplementedException();
		}

		public Vector3 GetLocationAtDistance(float d) {
			return sourceCurve.GetLocationAtDistance(Mathf.Min(startLength + d, sourceCurve.Length));
		}

		public Vector3 GetTangentAtDistance(float d) {
			return sourceCurve.GetTangentAtDistance(Mathf.Min(startLength + d, sourceCurve.Length));
		}

		public UnityEvent Changed => sourceCurve.Changed;
		public float Length => Mathf.Min(endLength - startLength, sourceCurve.Length);
	}

	private void Clear() {
		foreach (GameObject go in spawnedGos) {
			if (gameObject != null) {
				if (Application.isPlaying) {
					Destroy(go);
				} else {
					DestroyImmediate(go);
				}
			}
		}
		spawnedGos.Clear();
	}
}	
}

using UnityEngine;
using UnityEngine.Events;

namespace SplineMesh {
public interface ICurve {
	/// <summary>
	/// Returns point on curve at given time. Time must be between 0 and 1.
	/// </summary>
	/// <param name="t"></param>
	/// <returns></returns>
	Vector3 GetLocationAtTime(float t);

	/// <summary>
	/// Returns tangent of curve at given time. Time must be between 0 and 1.
	/// </summary>
	/// <param name="t"></param>
	/// <returns></returns>
	Vector3 GetTangentAtTime(float t);

	/// <summary>
	/// Returns point on curve at distance. Distance must be between 0 and curve length.
	/// </summary>
	/// <param name="d"></param>
	/// <returns></returns>
	Vector3 GetLocationAtDistance(float d);

	/// <summary>
	/// Returns tangent of curve at distance. Distance must be between 0 and curve length.
	/// </summary>
	/// <param name="d"></param>
	/// <returns></returns>
	Vector3 GetTangentAtDistance(float d);

	/// <summary>
	/// This event is raised when of of the control points has moved.
	/// </summary>
	UnityEvent Changed { get; }

	/// <summary>
	/// Length of the curve in world unit.
	/// </summary>
	float Length { get; }
}	
}

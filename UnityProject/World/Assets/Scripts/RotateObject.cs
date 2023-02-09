using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObject : MonoBehaviour
{
	[SerializeField] protected Vector3 m_RotateAxis = Vector3.up;
	[SerializeField] protected float m_AnglePerSecond = 360;

    // Update is called once per frame
    void Update()
    {
        this.transform.Rotate(m_RotateAxis * (m_AnglePerSecond * Time.deltaTime));
	}
}

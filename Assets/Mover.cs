using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour {
    public float speed = 3;
    public Transform A;
    public Transform B;

    private Transform target;
	// Use this for initialization
	void Start () {
        if(A)
        target = A;
	}

    bool reverse = false;
	// Update is called once per frame
	void Update () {
        if (!(A && B))
            return;

        if(Vector3.Distance(transform.position, target.position) > 1)
        {
            transform.position += (target.position - transform.position).normalized * speed ;
        }
        else
        {
            if (reverse)
            {
                target = A;
            }
            else
            {
                target = B;
            }
            reverse = !reverse;

        }

    }

    private void OnDrawGizmosSelected()
    {
        if(A && B)
        {
            Gizmos.DrawLine(A.transform.position, B.transform.position);
        }
    }
}

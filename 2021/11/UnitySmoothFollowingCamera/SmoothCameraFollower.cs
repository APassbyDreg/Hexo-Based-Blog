using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public GameObject targetObject;
    public Vector3 targetOffset;
    public Vector2 targetDirections;
    public float targetRotation;
    public float targetDistance = 5;

    public float L0Weight = 32;
    public float L1Weight = 16;
    [Range(0, 1)]
    public float interpThres = 0.2f;


    private Camera cam;
    private Vector3 objPos, objSpeed;
    private Vector3 camLookAt, camSpeed;
    private Vector3 realOffset, offsetSpeed;
    private Vector2 realDirections, directionSpeed;
    private float realRotation, rotationSpeed;
    private float realDistance, distanceSpeed;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();

        objPos = targetObject.transform.position;

        camLookAt = objPos;
        camSpeed = new Vector3(0.0f, 0.0f, 0.0f);
        realDirections = targetDirections;
        directionSpeed = new Vector2(0, 0);
        realRotation = targetRotation;
        rotationSpeed = 0;
        realDistance = targetDistance;
        distanceSpeed = 0;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateObjInfo();
        UpdateCamera();
    }

    private void UpdateObjInfo()
    {
        objSpeed = (targetObject.transform.position - objPos) / Time.deltaTime;
        objPos = targetObject.transform.position;

        Debug.Log(objSpeed);
    }

    private void UpdateCamera()
    {
        {
            Vector3 accel = L0Weight * (objPos - camLookAt) + L1Weight * (objSpeed - camSpeed);
            Vector3 dv = accel * Time.deltaTime;
            camLookAt += (camSpeed + dv * 0.5f) * Time.deltaTime;
            camSpeed += dv;
        }

        {
            Vector3 accel = L0Weight * (targetOffset - realOffset) + L1Weight * (-offsetSpeed);
            Vector3 dv = accel * Time.deltaTime;
            realOffset += (offsetSpeed + dv * 0.5f) * Time.deltaTime;
            offsetSpeed += dv;
        }

        {
            Vector2 accel = L0Weight * (targetDirections - realDirections) + L1Weight * (-directionSpeed);
            Vector2 dv = accel * Time.deltaTime;
            realDirections += (directionSpeed + dv * 0.5f) * Time.deltaTime;
            directionSpeed += dv;
        }

        {
            float accel = L0Weight * (targetRotation - realRotation) + L1Weight * (-rotationSpeed);
            float dv = accel * Time.deltaTime;
            realRotation += (rotationSpeed + dv * 0.5f) * Time.deltaTime;
            rotationSpeed += dv;
        }

        {
            float accel = L0Weight * (targetDistance - realDistance) + L1Weight * (-distanceSpeed);
            float dv = accel * Time.deltaTime;
            realDistance += (distanceSpeed + dv * 0.5f) * Time.deltaTime;
            distanceSpeed += dv;
        }

        Vector3 objCoord = sphereCoordToObjectCoord();
        cam.transform.position = camLookAt + objCoord + realOffset;
        cam.transform.LookAt(camLookAt + realOffset, realRotationsToWorldUp(objCoord));
    }

    private Vector3 sphereCoordToObjectCoord()
    {
        float a0 = Mathf.Deg2Rad * realDirections[0], a1 = Mathf.Deg2Rad * realDirections[1];
        return new Vector3(
            Mathf.Cos(a1) * Mathf.Cos(a0),
            Mathf.Sin(a1),
            Mathf.Cos(a1) * Mathf.Sin(a0)
        ) * realDistance;
    }

    private Vector3 realRotationsToWorldUp(Vector3 dir)
    {
        float a0 = Mathf.Deg2Rad * realDirections[0], a1 = Mathf.Deg2Rad * realDirections[1];
        float rot = Mathf.Deg2Rad * realRotation;

        Vector3 rotZ = dir.normalized;
        Vector3 rotX, rotY;
        if (Mathf.Abs(rotZ.y) < 1.0f)
        {
            Vector3 z = new Vector3(0, 1, 0);
            rotY = Vector3.Cross(z, rotZ).normalized;
            rotX = Vector3.Cross(rotZ, rotY);
        }
        else
        {
            rotX = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * -1;
            rotY = new Vector3(Mathf.Cos(a0), 0, -Mathf.Sin(a0)) * -1;
        }

        // smooth transition
        float diff = Mathf.Abs(realDirections[1] - 180 * Mathf.Floor(realDirections[1] / 180) - 90) / 90;
        if (diff < interpThres)
        {
            float interp = Mathf.Pow(1 - diff / interpThres, 2.5f);
            rot += Mathf.PI * interp * 0.5f * Mathf.Sign(Mathf.Cos(a1));
        }

        return rotX * Mathf.Cos(rot) + rotY * Mathf.Sin(rot);
    }

}

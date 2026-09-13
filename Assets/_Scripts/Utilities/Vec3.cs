using UnityEngine;

public static class Vec3
{
    public static Vector3 X(float value) => new Vector3(value,0,0);
    
    public static Vector3 Y(float value) => new Vector3(0,value,0);

    public static Vector3 Z(float value) => new Vector3(0,0,value);

}
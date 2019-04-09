using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackedRectangle : MonoBehaviour
{
    //// Start is called before the first frame update
    //void Start()
    //{

    //}

    //// Update is called once per frame
    //void Update()
    //{

    //}

    public int id;
    public int x;
    public int y;
    public int width;
    public int height;
    public Vector3 position;

    public TrackedRectangle(int id, int x, int y, int width, int height, Vector3 position)
    {
        this.id = id;
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        this.position = position;
    }
}

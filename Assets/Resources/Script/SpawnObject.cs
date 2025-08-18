using UnityEngine;

public class SpawnObject : MonoBehaviour
{
    public GameObject objectToSpawn;

    public void PlaceObject(Vector3 position)
    {
        Instantiate(objectToSpawn, position, Quaternion.identity);
    }
}

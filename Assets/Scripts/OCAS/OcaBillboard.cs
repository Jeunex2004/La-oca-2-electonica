using UnityEngine;

public class OcaBillboard : MonoBehaviour
{
    private Camera camaraPrincipal;

    void Start()
    {
        camaraPrincipal = Camera.main;
    }

    void LateUpdate()
    {
        if (camaraPrincipal != null)
        {
            // Hace que la imagen copie exactamente la rotación de la cámara para que siempre la mires de frente
            transform.rotation = camaraPrincipal.transform.rotation;
        }
    }
}
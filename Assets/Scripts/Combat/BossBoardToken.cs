using UnityEngine;
using Oca2.Board;

namespace Oca2.Combat
{
    /// <summary>
    /// Representa la figura 3D de un Boss (La Foca, La Roca o La Trollface)
    /// situada físicamente sobre su casilla del Tablero Principal (casillas 13, 31 y 52).
    /// </summary>
    public class BossBoardToken : MonoBehaviour
    {
        [SerializeField] private BossDefinition bossData;
        [SerializeField] private Renderer outerPedestalRenderer;
        [SerializeField] private Renderer innerPedestalRenderer;
        [SerializeField] private SpriteRenderer bossSpriteRenderer;

        private Vector3 baseSpriteLocalPos;
        private float animOffset;

        public BossDefinition BossData => bossData;

        public void Initialize(BossDefinition definition, BoardTile tile)
        {
            bossData = definition;
            animOffset = Random.Range(0f, Mathf.PI * 2f);
            gameObject.name = $"BossToken_{definition.tileIndex}_{definition.displayName}";

            BuildVisualHierarchy();

            if (bossSpriteRenderer != null && definition.sprite != null)
            {
                bossSpriteRenderer.sprite = definition.sprite;
                float spriteHeight = definition.sprite.bounds.size.y;
                if (spriteHeight > 0.01f)
                {
                    float desiredHeight = 1.45f;
                    float scale = desiredHeight / spriteHeight;
                    bossSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            if (tile != null)
            {
                transform.position = tile.GetStandPosition();
            }

            SetDefeated(definition.isDefeated);
        }

        private void BuildVisualHierarchy()
        {
            if (outerPedestalRenderer == null)
            {
                GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outer.name = "Pedestal_Exterior_Boss";
                outer.transform.SetParent(transform, false);
                outer.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                outer.transform.localScale = new Vector3(0.78f, 0.06f, 0.78f);
                Collider col = outer.GetComponent<Collider>();
                if (col != null) Destroy(col);
                outerPedestalRenderer = outer.GetComponent<Renderer>();
                outerPedestalRenderer.material = CreateMaterial(new Color(0.75f, 0.08f, 0.08f));
            }

            if (innerPedestalRenderer == null)
            {
                GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                inner.name = "Pedestal_Interior_Boss";
                inner.transform.SetParent(transform, false);
                inner.transform.localPosition = new Vector3(0f, 0.10f, 0f);
                inner.transform.localScale = new Vector3(0.56f, 0.06f, 0.56f);
                Collider col = inner.GetComponent<Collider>();
                if (col != null) Destroy(col);
                innerPedestalRenderer = inner.GetComponent<Renderer>();
                innerPedestalRenderer.material = CreateMaterial(new Color(0.18f, 0.05f, 0.05f));
            }

            if (bossSpriteRenderer == null)
            {
                GameObject spriteObj = new GameObject("Imagen_Boss");
                spriteObj.transform.SetParent(transform, false);
                baseSpriteLocalPos = new Vector3(0f, 0.85f, 0f);
                spriteObj.transform.localPosition = baseSpriteLocalPos;

                bossSpriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
                bossSpriteRenderer.sortingOrder = 10;

                if (spriteObj.GetComponent<OcaBillboard>() == null)
                {
                    spriteObj.AddComponent<OcaBillboard>();
                }
            }
        }

        private void Update()
        {
            if (bossSpriteRenderer != null && bossData != null && !bossData.isDefeated)
            {
                float bob = Mathf.Sin(Time.time * 2.6f + animOffset) * 0.06f;
                bossSpriteRenderer.transform.localPosition = baseSpriteLocalPos + new Vector3(0f, bob, 0f);
            }
        }

        public void SetDefeated(bool defeated)
        {
            if (bossData != null)
            {
                bossData.isDefeated = defeated;
            }
            gameObject.SetActive(!defeated);
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            return mat;
        }
    }
}

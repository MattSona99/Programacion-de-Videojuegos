using UnityEngine;
using UnityEditor;

public class ForestSower : MonoBehaviour
{
    [Header("Asset Alberi")]
    public GameObject[] prefabsAlberi;

    [Header("Impostazioni Cerchio")]
    public float raggioMinimo = 10f;
    public float raggioMassimo = 25f;
    public int quantita = 100;

    [Header("Variazioni")]
    public float scalaMinima = 0.8f;
    public float scalaMassima = 1.5f;

    [Header("Filtro Terreno")]
    [Tooltip("Inserisci qui il layer del terreno così il laser ignora alberi e player")]
    public LayerMask layerTerreno;

    [ContextMenu("Genera Foresta Permanente")]
    public void GeneraForesta()
    {
        if (prefabsAlberi.Length == 0) return;

        for (int i = 0; i < quantita; i++)
        {
            float angolo = Random.Range(0f, Mathf.PI * 2);
            float distanza = Mathf.Sqrt(Random.Range(raggioMinimo * raggioMinimo, raggioMassimo * raggioMassimo));
            
            Vector3 posizioneBase = transform.position + new Vector3(
                Mathf.Cos(angolo) * distanza,
                0, 
                Mathf.Sin(angolo) * distanza
            );

            Vector3 puntoInCielo = new Vector3(posizioneBase.x, 50f, posizioneBase.z);
            
            // IL LASER ORA USA IL FILTRO (layerTerreno)
            if (Physics.Raycast(puntoInCielo, Vector3.down, out RaycastHit hit, 100f, layerTerreno))
            {
                posizioneBase.y = hit.point.y;
            }
            else
            {
                // Se il laser non tocca il terreno (es. finisce fuori mappa), annulla questo fiore
                continue; 
            }

            GameObject alberoScelto = prefabsAlberi[Random.Range(0, prefabsAlberi.Length)];
            GameObject nuovoAlbero = (GameObject)PrefabUtility.InstantiatePrefab(alberoScelto);
            
            nuovoAlbero.transform.position = posizioneBase;
            nuovoAlbero.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            nuovoAlbero.transform.localScale = Vector3.one * Random.Range(scalaMinima, scalaMassima);
            nuovoAlbero.transform.parent = this.transform;
        }
        
        Debug.Log("Generazione intelligente completata!");
    }

    [ContextMenu("Pulisci Tutto")]
    public void Pulisci()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}
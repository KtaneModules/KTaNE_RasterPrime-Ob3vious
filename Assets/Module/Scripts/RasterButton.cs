using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RasterButton : MonoBehaviour
{
    public KMSelectable Selectable { get; private set; }

    [SerializeField]
    private MeshRenderer _referenceTile;

    private bool _enabled = true;
    private bool _allowedEnabled = true;

    public delegate void RasterInteraction();
    public RasterInteraction OnInteract { get; set; }

    void Awake()
    {
        Selectable = GetComponentInChildren<KMSelectable>();
        Disable();
    }

    void Start()
    {
        Selectable.OnInteract = () =>
        {
            if (!_enabled)
                return false;

            OnInteract();

            return false;
        };
    }

    public void InstantiateTiles(RasterShape shape)
    {
        _referenceTile.enabled = true;

        _referenceTile.material.color = RasterPrimeScript.ActiveColor;

        float hOffset = -1;
        float vOffset = -1;

        for (int i = 0; i < 3; i++)
        {
            bool covered = false;
            for (int j = 0; j < 3; j++)
                covered |= shape.OccupiedTiles[i, j];

            if (covered)
                break;

            vOffset -= 0.5f;
        }

        for (int i = 0; i < 3; i++)
        {
            bool covered = false;
            for (int j = 0; j < 3; j++)
                covered |= shape.OccupiedTiles[j, i];

            if (covered)
                break;

            hOffset -= 0.5f;
        }

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (shape.OccupiedTiles[i, j])
                {
                    MeshRenderer copy = Instantiate(_referenceTile, _referenceTile.transform.parent);
                    copy.transform.localPosition = new Vector3(j + hOffset, i + vOffset);
                }

        _referenceTile.enabled = false;
    }

    public IEnumerator InteractionAnimation(Vector3 offset)
    {
        Color color1 = new Color32(0x40, 0xE0, 0xE0, 0xFF);
        Color color2 = new Color32(0x40, 0xE0, 0xE0, 0x00);

        Transform copy = Instantiate(_referenceTile.transform.parent, _referenceTile.transform.parent.parent);
        Vector3 position1 = copy.localPosition;
        Vector3 position2 = position1 + offset;

        MeshRenderer[] renderers = copy.GetComponentsInChildren<MeshRenderer>().Where(x => x.enabled).ToArray();

        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / 0.5f;
            foreach (MeshRenderer renderer in renderers)
                renderer.material.color = Color.Lerp(color1, color2, Mathf.Min(t, 1));
            copy.localPosition = Vector3.Lerp(position1, position2, Mathf.Min(t, 1));
            copy.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 2, Mathf.Min(t, 1));
            yield return null;
        }

        Destroy(copy.gameObject);
    }

    public void Enable()
    {
        if (_enabled || !_allowedEnabled)
            return;

        _enabled = true;

        _referenceTile.transform.parent.localScale = Vector3.one;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        _enabled = false;

        _referenceTile.transform.parent.localScale = Vector3.zero;
    }

    public IEnumerator PermanentDisable()
    {
        Disable();
        Selectable.transform.localScale = Vector3.zero;
        _allowedEnabled = false;

        Color color1 = RasterPrimeScript.ActiveColor;
        Color color2 = new Color(color1.r, color1.g, color1.b, 0);

        MeshRenderer[] renderers = _referenceTile.transform.parent.GetComponentsInChildren<MeshRenderer>().Where(x => x.enabled).ToArray();

        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / 0.5f;
            foreach (MeshRenderer renderer in renderers)
                renderer.material.color = Color.Lerp(color1, color2, Mathf.Min(t, 1));

            foreach (MeshRenderer renderer in renderers)
                renderer.enabled = _enabled;

            yield return null;
        }
    }
}

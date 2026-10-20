using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Solitaire.Services
{
    public interface IHudTuck
    {
        UniTask TuckAsync(RectTransform keepVisible = null);
        void Restore(bool instant = true);
    }
}

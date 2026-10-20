using System.Collections.Generic;
using Solitaire.Models;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Solitaire.Services
{
    public class DragAndDropHandler : IDragAndDropHandler
    {
        private const int DragOrder = 100;
        private readonly Camera _camera;
        private IList<Card> _draggedCards;

        public DragAndDropHandler()
        {
            _camera = Camera.main;
            _draggedCards = new List<Card>();
        }

        public void BeginDrag(PointerEventData eventData, IList<Card> draggedCards)
        {
            var dragPos = PointerToWorldPoint(eventData);
            _draggedCards = draggedCards;

            for (var i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];
                card.DragOrigin = card.Position.Value;
                card.DragOffset = card.DragOrigin - dragPos;
                card.OrderToRestore = card.Pile.Cards.IndexOf(card);
                card.Order.Value = DragOrder + i;
                card.IsDragged = true;
            }
        }

        public void Drag(PointerEventData eventData)
        {
            var dragPos = PointerToWorldPoint(eventData);

            for (var i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];
                card.Position.Value = dragPos + card.DragOffset;
            }
        }

        public void Drop()
        {
            for (var i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];
                card.IsDragged = false;
            }
        }

        public void EndDrag()
        {
            ReleaseDraggedCards(returnHome: true);
        }

        public void EndDragKeepPlace()
        {
            ReleaseDraggedCards(returnHome: false);
        }

        public void ReturnDraggedHome()
        {
            if (_draggedCards == null)
                return;

            for (var i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];
                card.Position.Value = card.DragOrigin;
            }
        }

        private void ReleaseDraggedCards(bool returnHome)
        {
            for (var i = 0; i < _draggedCards.Count; i++)
            {
                var card = _draggedCards[i];

                if (!card.IsDragged)
                    continue;

                card.IsDragged = false;
                if (returnHome)
                {
                    card.Order.Value = card.OrderToRestore;
                    card.Position.Value = card.DragOrigin;
                }
                // Keep drag sorting order while the reject jelly plays so the
                // held card stays above the pile it was dropped on.
            }
        }

        private Vector3 PointerToWorldPoint(PointerEventData eventData)
        {
            var screenPoint = new Vector3(
                eventData.position.x,
                eventData.position.y,
                -_camera.transform.position.z
            );
            return _camera.ScreenToWorldPoint(screenPoint);
        }
    }
}

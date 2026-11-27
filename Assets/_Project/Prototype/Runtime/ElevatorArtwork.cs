using System;
using System.Collections.Generic;
using UnityEngine;

namespace Etazh.Prototype
{
    public sealed class ElevatorArtwork : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;
        private const int BoardWidth = 318;
        private const int BoardHeight = 223;
        private const int RenderScale = 3;
        internal const int MaxVisibleInQueue = 4;
        private const float QueueEnd = 266f;
        private const float QueueSpacing = 18f;
        private const float FloorLine = 37f;
        private const int PatienceTrackWidth = 14;
        private const int PatienceOrder = 58;
        private const int WalkRightStart = 4;
        private const int WalkLeftStart = 10;
        private const int WalkFrames = 6;
        private const int CourierCast = 5;
        private const int MoverCast = 6;
        private const int DepartureDisplayTicks = 17;
        private const double DoorAnimationTicks = 2d;
        private const double ExitCabinStart = 1.1d;
        private const double ExitCabinEnd = 2.8d;
        private const double EnterCabinTicks = 3.1d;
        private const float HallwayPortion = 0.62f;
        private const float ClearanceInFrontOfSill = 2f;
        private const float StepToAisleDistance = 3f;
        private const int InFrontOfDoorOrder = 62;
        private const double ReachApartment = 18d;
        private const double InsideApartment = 20d;
        private const double ApartmentClosed = 23d;
        private static readonly RectInt[] PortraitRects =
        {
            new(5, 2, 14, 11), new(6, 2, 12, 11), new(6, 1, 11, 10),
            new(6, 1, 12, 12), new(5, 6, 14, 8), new(5, 1, 14, 12), new(6, 2, 12, 11)
        };

        [SerializeField] private Camera _camera;
        [SerializeField] private Transform[] _floors;
        [SerializeField] private SpriteRenderer[] _doors;
        [SerializeField] private Sprite[] _doorFrames;
        [SerializeField] private SpriteRenderer _cabin;
        [SerializeField] private Sprite[] _yellow;
        [SerializeField] private Sprite[] _average;
        [SerializeField] private Sprite[] _slim;
        [SerializeField] private Sprite[] _stocky;
        [SerializeField] private Sprite[] _child;
        [SerializeField] private Sprite[] _blueApartment;
        [SerializeField] private Sprite[] _redApartment;
        [SerializeField] private Sprite[] _oliveApartment;
        [SerializeField] private Sprite[] _ochreApartment;

        private sealed class ApartmentDoor
        {
            private readonly SpriteRenderer _renderer;
            private readonly Sprite[] _frames;
            private int _wanted;

            public ApartmentDoor(SpriteRenderer renderer, Sprite[] frames, Transform floor)
            {
                _renderer = renderer;
                _frames = frames;
                Vector3 local = floor.InverseTransformPoint(renderer.transform.position);
                X = local.x * PixelsPerUnit;
                Threshold = -local.y * PixelsPerUnit;
            }

            public float X { get; }
            public float Threshold { get; }

            public void Open(float progress)
                => _wanted = Mathf.Max(_wanted, Mathf.RoundToInt(progress * (_frames.Length - 1)));

            public void Render()
            {
                if (_renderer.sprite != _frames[_wanted])
                    _renderer.sprite = _frames[_wanted];
                _wanted = 0;
            }
        }

        private readonly List<SpriteRenderer> _people = new();
        private readonly List<(SpriteRenderer Track, SpriteRenderer Fill)> _patienceMeters = new();
        private readonly Dictionary<PassengerCall, int> _waitingSlots = new();
        private RenderTexture _target;
        private Sprite _patiencePixel;
        private SpriteRenderer _travelCabin;
        private Sprite[][] _casts;
        private Sprite[] _portraits;
        private ApartmentDoor[][] _apartments;
        private Vector2[] _landingSills;
        private Vector2 _cabinScale;
        private Vector2 _cabinInteriorOffset;
        private Vector3 _cabinOffset;
        private float _lobbyEntranceX;
        private ShiftModel _displayedShift;
        private int _previousOpenFloor;
        private int _visible;
        private int _visiblePatienceMeters;

#if UNITY_EDITOR
        internal Bounds PreviewBounds
        {
            get
            {
                if (_camera == null)
                    return default;

                Vector3 center = _camera.transform.position;
                center.z = transform.position.z;
                return new Bounds(center, new Vector3(BoardWidth / PixelsPerUnit, BoardHeight / PixelsPerUnit, 0));
            }
        }
#endif

        internal RenderTexture Initialize()
        {
            const int cabinReferenceFloor = 2;
            Transform cabinRoot = _cabin.transform.parent;
            _cabinOffset = cabinRoot.localPosition - _floors[cabinReferenceFloor].localPosition;
            _cabinScale = _cabin.transform.localScale;
            _cabinInteriorOffset = _cabin.transform.localPosition * PixelsPerUnit;
            _camera.orthographicSize = BoardHeight / (2 * PixelsPerUnit);
            _target = new RenderTexture(BoardWidth * RenderScale, BoardHeight * RenderScale, 24)
            {
                name = "Elevator artwork",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _target.Create();
            _camera.targetTexture = _target;
            _patiencePixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(0, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            CacheApartmentDoors();
            _casts = new[]
            {
                _yellow, _average, _slim, _stocky, _child,
                LoadEventFrames("courier", _yellow), LoadEventFrames("mover", _average)
            };
            _portraits = new Sprite[_casts.Length];
            for (int i = 0; i < _casts.Length; i++)
            {
                Sprite idle = _casts[i][0];
                Rect region = idle.rect;
                RectInt face = PortraitRects[i];
                _portraits[i] = Sprite.Create(idle.texture,
                    new Rect(region.x + face.x, region.yMax - face.yMax, face.width, face.height),
                    new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            _travelCabin = new GameObject("Кабина в пути").AddComponent<SpriteRenderer>();
            Transform landingDoor = _doors[cabinReferenceFloor].transform;
            _travelCabin.transform.SetParent(cabinRoot, false);
            _travelCabin.transform.localPosition = cabinRoot.InverseTransformPoint(landingDoor.position);
            _travelCabin.transform.localScale = landingDoor.localScale;
            _travelCabin.sprite = _doorFrames[0];
            _travelCabin.sharedMaterial = _cabin.sharedMaterial;
            _travelCabin.color = new Color32(174, 251, 255, 255);
            _travelCabin.sortingOrder = 61;
            _travelCabin.enabled = false;

            _landingSills = new Vector2[_doors.Length];
            for (int i = 0; i < _doors.Length; i++)
            {
                SpriteRenderer door = _doors[i];
                door.sprite = _doorFrames[0];
                Bounds bounds = door.bounds;
                Vector3 sill = _floors[i].InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                _landingSills[i] = new Vector2(sill.x, -sill.y) * PixelsPerUnit;
            }

            return _target;
        }

        internal void Draw(ShiftModel model, double partialTick)
        {
            if (_displayedShift != model)
            {
                _displayedShift = model;
                _waitingSlots.Clear();
            }

            double time = model.Tick + (model.Finished ? 0 : partialTick);
            bool doorsOpen = model.State == LiftState.DoorsOpen && !model.Finished;
            int openFloor = doorsOpen ? model.CurrentFloor : 0;

            if (_previousOpenFloor != 0 && _previousOpenFloor != openFloor)
                _doors[_previousOpenFloor - 1].sprite = _doorFrames[0];

            if (doorsOpen)
            {
                double opening = (time - model.DoorsOpenedAt) / DoorAnimationTicks;
                double closing = (model.DoorsCloseAt - time) / DoorAnimationTicks;
                float progress = Mathf.Clamp01((float)Math.Min(opening, closing));
                int frame = Mathf.RoundToInt(progress * (_doorFrames.Length - 1));
                _doors[openFloor - 1].sprite = _doorFrames[frame];
            }
            _previousOpenFloor = openFloor;

            float floorPosition = (float)model.LiftPosition(partialTick);
            int lower = Mathf.Clamp(Mathf.FloorToInt(floorPosition), 1, _floors.Length);
            int upper = Mathf.Min(lower + 1, _floors.Length);
            _cabin.transform.parent.localPosition = _cabinOffset
                + Vector3.Lerp(_floors[lower - 1].localPosition, _floors[upper - 1].localPosition, floorPosition - lower);
            _cabin.enabled = doorsOpen;
            _travelCabin.enabled = model.State == LiftState.Moving && !model.Finished;

            int previouslyVisible = _visible;
            _visible = 0;
            int previousPatienceMeters = _visiblePatienceMeters;
            _visiblePatienceMeters = 0;
            for (int floor = 1; floor <= _floors.Length; floor++)
                DrawFloor(model, floor, time);

            if (doorsOpen)
                DrawCabin(model, time);

            foreach (var floor in _apartments)
                foreach (ApartmentDoor door in floor)
                    door.Render();

            for (int i = _visible; i < previouslyVisible; i++)
                _people[i].enabled = false;
            for (int i = _visiblePatienceMeters; i < previousPatienceMeters; i++)
            {
                _patienceMeters[i].Track.enabled = false;
                _patienceMeters[i].Fill.enabled = false;
            }
        }

        private void DrawFloor(ShiftModel model, int floor, double time)
        {
            Transform parent = _floors[floor - 1];
            Span<int> indices = stackalloc int[MaxVisibleInQueue];
            int visibleQueue = GetVisibleWaitingIndices(model, floor, indices);
            for (int slot = 0; slot < visibleQueue; slot++)
            {
                int index = indices[slot];
                PassengerCall passenger = model.WaitingPassengerAt(floor, index);
                _waitingSlots[passenger] = slot;
                Sprite sprite = FramesFor(passenger)[IdleFrame(time, passenger.Tick)];
                float x = QueueEnd - slot * QueueSpacing;
                Place(parent, sprite, x, FloorLine, 1, 45);
                if (!model.Finished)
                {
                    float remaining = (float)model.PatienceRemainingAt(floor, index) / passenger.PatienceTicks;
                    DrawPatienceMeter(parent, sprite, x, remaining);
                }
            }

            if (model.Finished)
                return;

            if (model.TryGetApproach(floor, out var approaching, out _))
            {
                int slot = approaching.Kind == PassengerKind.Resident ? visibleQueue : Mathf.Min(visibleQueue, MaxVisibleInQueue - 1);
                DrawApartmentExit(approaching, parent, slot, time);
            }

            DrawAlightings(model, floor, parent, time);

            int departure = model.LastDepartureAt(floor);
            if (departure >= 0 && time - departure < DepartureDisplayTicks)
            {
                float x = Mathf.Lerp(QueueEnd, 63, (float)(time - departure) / DepartureDisplayTicks);
                PassengerCall passenger = model.DepartedPassengerAt(floor);
                Place(parent, WalkFrame(passenger, time, WalkLeftStart), x, FloorLine, 1, 47);
            }
        }

        internal static int GetVisibleWaitingIndices(ShiftModel model, int floor, Span<int> indices)
        {
            int waiting = model.WaitingAt(floor);
            int ordinarySlots = indices.Length;
            if (waiting > indices.Length)
            {
                for (int i = 0; i < waiting; i++)
                    if (model.WaitingPassengerAt(floor, i).Kind != PassengerKind.Resident)
                        ordinarySlots--;
            }

            int visible = 0;
            for (int i = 0; i < waiting && visible < indices.Length; i++)
            {
                if (model.WaitingPassengerAt(floor, i).Kind == PassengerKind.Resident && ordinarySlots-- <= 0)
                    continue;
                indices[visible++] = i;
            }
            return visible;
        }

        private void DrawPatienceMeter(Transform floor, Sprite passenger, float x, float remaining)
        {
            if (_visiblePatienceMeters == _patienceMeters.Count)
                _patienceMeters.Add((CreatePatienceSprite("Шкала терпения", PatienceOrder),
                    CreatePatienceSprite("Запас терпения", PatienceOrder + 1)));

            var meter = _patienceMeters[_visiblePatienceMeters++];
            float y = Mathf.Max(5, Mathf.Round(FloorLine - passenger.bounds.max.y * PixelsPerUnit - 3));
            float left = x - PatienceTrackWidth / 2f;
            PositionPatienceSprite(meter.Track, floor, left, y, PatienceTrackWidth, 4);
            meter.Track.color = new Color32(8, 19, 34, 255);

            int width = Mathf.CeilToInt((PatienceTrackWidth - 2) * remaining);
            PositionPatienceSprite(meter.Fill, floor, left + 1, y, width, 2);
            meter.Fill.color = remaining <= 0.25f ? new Color32(255, 100, 92, 255)
                : remaining <= 0.5f ? new Color32(255, 192, 78, 255)
                : new Color32(87, 218, 194, 255);
        }

        private SpriteRenderer CreatePatienceSprite(string name, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = _patiencePixel;
            renderer.sharedMaterial = _cabin.sharedMaterial;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void PositionPatienceSprite(SpriteRenderer renderer, Transform floor,
            float x, float y, int width, int height)
        {
            if (renderer.transform.parent != floor)
                renderer.transform.SetParent(floor, false);
            renderer.transform.localPosition = new Vector3(x / PixelsPerUnit, -y / PixelsPerUnit, 0);
            renderer.transform.localScale = new Vector3(width, height, 1);
            renderer.enabled = width > 0;
        }

        private void DrawCabin(ShiftModel model, double time)
        {
            int count = model.OnBoardCount;
            for (int i = 0; i < count; i++)
            {
                PassengerCall passenger = model.RidingPassengerAt(i);
                var seat = CabinSeat(count, i, passenger);
                var boarding = model.BoardingAt(i);
                double start = Math.Max(boarding.Tick, model.DoorsOpenedAt + ExitCabinStart);
                var alightings = model.AlightingsAt(model.CurrentFloor);
                if (alightings.Count > 0)
                    start = Math.Max(start, alightings[0].Tick + ExitCabinEnd);
                if (boarding.Tick >= model.DoorsOpenedAt && time < start + EnterCabinTicks)
                {
                    float progress = Progress(time, start, start + EnterCabinTicks);
                    int slot = _waitingSlots.TryGetValue(passenger, out int lastSlot)
                        ? lastSlot : Mathf.Min(boarding.QueueIndex, MaxVisibleInQueue - 1);
                    float queueX = QueueEnd - slot * QueueSpacing;
                    Sprite sprite = progress > 0 ? WalkFrame(passenger, time, WalkRightStart)
                        : FramesFor(passenger)[IdleFrame(time, passenger.Tick)];
                    PlaceCabinTransfer(model.CurrentFloor, sprite, queueX, seat, progress, i);
                    continue;
                }

                Place(_cabin.transform.parent, FramesFor(passenger)[IdleFrame(time, passenger.Tick)],
                    seat.X, seat.Foot, seat.Scale, 48 + i);
            }
        }

        private void CacheApartmentDoors()
        {
            Sprite[][] framesets = { _blueApartment, _redApartment, _oliveApartment, _ochreApartment };
            var framesByClosedSprite = new Dictionary<Sprite, Sprite[]>();
            foreach (Sprite[] frames in framesets)
            {
                if (frames != null && frames.Length > 1)
                    framesByClosedSprite.Add(frames[0], frames);
            }

            _apartments = new ApartmentDoor[_floors.Length][];
            for (int index = 0; index < _floors.Length; index++)
            {
                Transform floor = _floors[index];
                var doors = new List<ApartmentDoor>();
                foreach (SpriteRenderer renderer in floor.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (renderer.sprite == null)
                        continue;
                    if (index == 0 && renderer.sprite.name == "building_entrance")
                        _lobbyEntranceX = floor.InverseTransformPoint(renderer.bounds.center).x * PixelsPerUnit;
                    if (framesByClosedSprite.TryGetValue(renderer.sprite, out var frames))
                        doors.Add(new ApartmentDoor(renderer, frames, floor));
                }
                doors.Sort((left, right) => left.X.CompareTo(right.X));
                _apartments[index] = doors.ToArray();
            }
        }

        private ApartmentDoor ApartmentFor(PassengerCall passenger, int floor)
        {
            var doors = _apartments[floor - 1];
            if (doors.Length == 0)
                return null;
            if (passenger.Kind == PassengerKind.PartyGuest && passenger.Origin == floor)
                return doors[0];
            int seed = passenger.Tick / ShiftRules.TicksPerSecond + passenger.Origin * 7 + passenger.Destination * 11;
            return doors[seed % doors.Length];
        }

        private void DrawApartmentExit(PassengerCall passenger, Transform floor, int queued, double time)
        {
            double elapsed = time - (passenger.Tick - ShiftRules.ApproachTicks);
            bool fromLobby = passenger.Origin == 1 && passenger.Kind is PassengerKind.Mover or PassengerKind.Courier;
            ApartmentDoor apartment = fromLobby ? null : ApartmentFor(passenger, passenger.Origin);
            apartment?.Open(Mathf.Min(Progress(elapsed, 0, 2.7), 1 - Progress(elapsed, 6, 10)));
            float visibility = Progress(elapsed, 0.8, 2.8);
            if (visibility <= 0)
                return;

            float threshold = apartment?.Threshold ?? FloorLine;
            float route = Progress(elapsed, 2.5, ShiftRules.ApproachTicks);
            float entranceX = fromLobby ? _lobbyEntranceX : apartment?.X ?? 132;
            float x = Mathf.Lerp(entranceX, QueueEnd - queued * QueueSpacing, route);
            float step = Progress(elapsed, 1.3, 5.6);
            Place(floor, WalkFrame(passenger, time, WalkRightStart), x, Mathf.Lerp(threshold, FloorLine, step),
                Mathf.Lerp(0.78f, 1, step), 46, visibility);
        }

        private void DrawAlightings(ShiftModel model, int floorNumber, Transform floor, double time)
        {
            var alightings = model.AlightingsAt(floorNumber);
            for (int i = 0; i < alightings.Count; i++)
            {
                var exit = alightings[i];
                double elapsed = time - exit.Tick;
                if (elapsed >= ApartmentClosed)
                    continue;

                if (elapsed < ExitCabinEnd)
                {
                    var seat = CabinSeat(exit.CabinCount, exit.Seat, exit.Passenger);
                    float step = Progress(elapsed, ExitCabinStart, ExitCabinEnd);
                    Sprite sprite = step > 0 ? WalkFrame(exit.Passenger, time, WalkLeftStart)
                        : FramesFor(exit.Passenger)[IdleFrame(time, exit.Passenger.Tick)];
                    PlaceCabinTransfer(floorNumber, sprite, QueueEnd - i * 9, seat, 1 - step, i);
                    continue;
                }

                bool toLobby = exit.Passenger.Kind == PassengerKind.PartyGuest && floorNumber == 1;
                ApartmentDoor apartment = toLobby ? null : ApartmentFor(exit.Passenger, floorNumber);
                apartment?.Open(Mathf.Min(Progress(elapsed, 14.5, 17.5), 1 - Progress(elapsed, InsideApartment, ApartmentClosed)));
                if (elapsed >= InsideApartment)
                    continue;

                float approach = Progress(elapsed, ExitCabinEnd, ReachApartment);
                float enter = Progress(elapsed, ReachApartment, InsideApartment);
                Place(floor, WalkFrame(exit.Passenger, time, WalkLeftStart),
                    Mathf.Lerp(QueueEnd - i * 9, toLobby ? _lobbyEntranceX : apartment?.X ?? 132, approach),
                    Mathf.Lerp(FloorLine, apartment?.Threshold ?? FloorLine, enter),
                    Mathf.Lerp(1, 0.76f, enter), 54, 1 - enter);
            }
        }

        private void PlaceCabinTransfer(int floor, Sprite sprite, float hallX,
            (float X, float Foot, float Scale) seat, float progress, int index)
        {
            Vector2 sill = _landingSills[floor - 1];
            Vector2 interior = new(_cabinOffset.x * PixelsPerUnit + seat.X, -_cabinOffset.y * PixelsPerUnit + seat.Foot);
            Vector2 outside = new(interior.x, Mathf.Max(FloorLine, sill.y + ClearanceInFrontOfSill));
            Vector2 position;
            float scale = 1;
            if (progress < HallwayPortion)
            {
                float x = Mathf.Lerp(hallX, outside.x, progress / HallwayPortion);
                float aisle = Mathf.Clamp01((x - hallX) / StepToAisleDistance);
                position = new Vector2(x, Mathf.Lerp(FloorLine, outside.y, aisle));
            }
            else
            {
                float stepInside = Progress(progress, HallwayPortion, 1);
                position = Vector2.Lerp(outside, interior, stepInside);
                scale = Mathf.Lerp(1, seat.Scale, stepInside);
            }

            // The foreground aisle passes in front of the jambs. Inside the opening, the door covers riders.
            int order = progress < HallwayPortion || position.y > sill.y ? InFrontOfDoorOrder + index : 54 + index;
            Place(_floors[floor - 1], sprite, position.x, position.y, scale, order);
        }

        private (float X, float Foot, float Scale) CabinSeat(int count, int index, PassengerCall passenger)
        {
            bool singleInRow = count == 1 || (count == 3 && index == 2);
            float x = singleInRow ? 17 : 13 + index % 2 * 8;
            float foot = count > 2 && index < 2 ? 20 : 28;
            float scale = count == 1 ? 1 : passenger.WeightKg >= 150 ? 0.72f : 0.82f;
            return (x * _cabinScale.x + _cabinInteriorOffset.x, foot * _cabinScale.y - _cabinInteriorOffset.y,
                count > 2 ? Mathf.Min(scale, 0.76f) : scale);
        }

        private static float Progress(double value, double start, double end)
            => Mathf.Clamp01((float)((value - start) / (end - start)));

        internal Sprite PortraitFor(PassengerCall passenger) => _portraits[StyleOf(passenger)];

        private Sprite[] FramesFor(PassengerCall passenger) => _casts[StyleOf(passenger)];

        private static Sprite[] LoadEventFrames(string name, Sprite[] fallback)
        {
            Sprite[] frames = Resources.LoadAll<Sprite>($"Events/{name}");
            if (frames.Length != WalkLeftStart + WalkFrames)
                return fallback;
            Array.Sort(frames, static (left, right) => string.CompareOrdinal(left.name, right.name));
            return frames;
        }

        private static int StyleOf(PassengerCall passenger)
        {
            if (passenger.Kind == PassengerKind.Courier) return CourierCast;
            if (passenger.Kind == PassengerKind.Mover) return MoverCast;
            if (passenger.WeightKg >= 150) return 3;
            if (passenger.WeightKg >= 100) return 1;
            return ((passenger.Tick / ShiftRules.TicksPerSecond + passenger.Origin + passenger.Destination) % 3) switch
            {
                0 => 4,
                1 => 2,
                _ => 0
            };
        }

        private static int IdleFrame(double time, int seed)
        {
            int phase = (int)((time + seed) * 2) % 44;
            return phase >= 41 ? phase - 40 : 0;
        }

        private Sprite WalkFrame(PassengerCall passenger, double time, int direction)
        {
            int phase = (int)(time * (passenger.WeightKg >= 150 ? 0.8 : 1)) % WalkFrames;
            return FramesFor(passenger)[direction + phase];
        }

        private void Place(Transform parent, Sprite sprite, float x, float y, float scale, int order, float alpha = 1)
        {
            if (_visible == _people.Count)
            {
                var renderer = new GameObject("Пассажир").AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = _cabin.sharedMaterial;
                _people.Add(renderer);
            }

            SpriteRenderer person = _people[_visible++];
            if (person.transform.parent != parent)
                person.transform.SetParent(parent, false);
            person.transform.localPosition = new Vector3(x / PixelsPerUnit, -y / PixelsPerUnit, 0);
            person.transform.localScale = Vector3.one * scale;
            person.sprite = sprite;
            person.color = new Color(1, 1, 1, alpha);
            person.sortingOrder = order;
            person.enabled = true;

        }

        private void OnDestroy()
        {
            if (_target == null)
                return;
            if (_camera != null)
                _camera.targetTexture = null;
            _target.Release();
            Destroy(_target);
            Destroy(_patiencePixel);
            foreach (Sprite portrait in _portraits)
                Destroy(portrait);
        }
    }
}

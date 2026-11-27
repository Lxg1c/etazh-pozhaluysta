using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Etazh.Prototype
{
    internal sealed class PrototypeView
    {
        private const string ControlsHint = "Остановки выбираются мышью или клавишами 1–5.";
        private const int EventHintTailTicks = 6 * ShiftRules.TicksPerSecond;

        private readonly bool _artMode;
        private readonly Func<PassengerCall, Sprite> _portrait;
        private readonly Font _displayFont;
        private readonly Label _clock;
        private readonly Label _phase;
        private readonly Label _score;
        private readonly Label _record;
        private readonly Label _satisfaction;
        private readonly VisualElement _satisfactionMeter;
        private readonly VisualElement _satisfactionPointer;
        private readonly Texture2D[] _satisfactionFaces;
        private readonly IVisualElementScheduledItem _settleSatisfactionFace;
        private int _lastSatisfaction = -1;
        private readonly Label _status;
        private readonly Label _cabinContents;
        private readonly Label _cabinRoutes;
        private readonly Label _weight;
        private readonly Label _space;
        private readonly VisualElement _weightFill;
        private readonly VisualElement _spaceFill;
        private readonly VisualElement[] _approaching = new VisualElement[ShiftRules.FloorCount];
        private readonly VisualElement[] _waitingPeople = new VisualElement[ShiftRules.FloorCount];
        private readonly Label[] _transfers = new Label[ShiftRules.FloorCount];
        private readonly Label _summary;
        private readonly Label _shiftHint;
        private (int From, int Until, string Text)[] _eventHints = Array.Empty<(int, int, string)>();
        private readonly VisualElement _shiftProgress;
        private readonly VisualElement _cabin;
        private readonly Label _artLiftMarker;
        private readonly VisualElement _overlay;
        private readonly Label _overlayTitle;
        private readonly Label _overlayBody;
        private readonly Button _start;
        private readonly Button _pause;
        private readonly Button _resume;
        private readonly VisualElement _pauseOverlay;
        private readonly VolumeControl _pauseVolume;
        private readonly Label[] _queue = new Label[ShiftRules.FloorCount];
        private readonly Label[] _stairs = new Label[ShiftRules.FloorCount];
        private readonly Label[] _eventNotices = new Label[ShiftRules.FloorCount];
        private readonly Button[] _buttons = new Button[ShiftRules.FloorCount];
        private readonly Label[] _floorStates = new Label[ShiftRules.FloorCount];
        private readonly VisualElement[] _riderPortraits = new VisualElement[ShiftRules.FloorCount];

        private sealed class WaitingCard : VisualElement
        {
            internal readonly Label Load;
            internal readonly Label Reward;
            internal readonly VisualElement Patience;

            internal WaitingCard(bool showPatience)
            {
                AddToClassList("passenger-card");
                VisualElement details = AddBox(this, "passenger-details");
                Load = AddLabel(details, "", "passenger-load");
                Reward = AddLabel(details, "", "passenger-reward");
                if (showPatience)
                    Patience = AddBox(AddBox(this, "patience-track"), "patience-fill");
            }
        }

        public PrototypeView(VisualElement root, Action<int> selectFloor, Action start,
            RenderTexture artwork = null, Func<PassengerCall, Sprite> portrait = null, bool scenePreview = false,
            Action returnToMenu = null, Action togglePause = null)
        {
            _artMode = artwork != null || scenePreview;
            _portrait = portrait;
            root.styleSheets.Add(Resources.Load<StyleSheet>("ElevatorPrototype"));
            root.AddToClassList("screen");
            if (_artMode)
            {
                root.AddToClassList("art-screen");
                _displayFont = Resources.Load<Font>("Fonts/PressStart2P-Regular");
            }
            VisualElement header = AddBox(root, "header");
            VisualElement counters = header;
            if (_artMode)
            {
                counters = AddBox(header, "art-counters");
                _phase = new Label();
                _phase.AddToClassList("phase");
            }
            else
            {
                VisualElement title = AddBox(header, "heading");
                AddLabel(title, "Этаж, пожалуйста", "title");
                _phase = AddLabel(title, "", "phase");
            }

            VisualElement clock = AddBox(counters, "clock");
            if (!_artMode)
                AddLabel(clock, "ДО КОНЦА СМЕНЫ", "eyebrow");
            _clock = AddLabel(clock, "", "clock-value");
            _clock.name = "clock";
            VisualElement shiftTrack = AddBox(clock, "shift-track");
            _shiftProgress = AddBox(shiftTrack, "shift-progress");

            VisualElement score = AddBox(counters, "score");
            AddLabel(score, "СЧЁТ", "muted");
            _score = AddLabel(score, "0", "score-value");
            _score.name = "score";
            if (_artMode)
            {
                var record = AddBox(counters, "art-record");
                AddLabel(record, "РЕКОРД", "muted");
                _record = AddLabel(record, "0", "score-value");
                _record.name = "record";
                ApplyDisplayFont(counters);
            }

            _satisfactionMeter = AddBox(header, "satisfaction");
            if (_artMode)
            {
                _satisfactionMeter.tooltip = "Довольство пассажиров";
                var satisfactionTrack = AddBox(_satisfactionMeter, "satisfaction-track");
                satisfactionTrack.name = "satisfaction-track";
                satisfactionTrack.style.backgroundImage = Resources.Load<Texture2D>("Hud/mood-gradient");
                _satisfactionPointer = AddBox(satisfactionTrack, "satisfaction-pointer");
                _satisfactionPointer.pickingMode = PickingMode.Ignore;
                _satisfactionFaces = new[]
                {
                    Resources.Load<Texture2D>("Hud/mood-unhappy"),
                    Resources.Load<Texture2D>("Hud/mood-neutral"),
                    Resources.Load<Texture2D>("Hud/mood-happy")
                };
                _settleSatisfactionFace = _satisfactionPointer.schedule.Execute(() =>
                    _satisfactionPointer.style.scale = new Scale(Vector3.one));
                _settleSatisfactionFace.Pause();
                _satisfaction = AddLabel(_satisfactionMeter, "", "satisfaction-value");
                ApplyDisplayFont(_satisfaction);
            }
            else
            {
                VisualElement heading = AddBox(_satisfactionMeter, "satisfaction-heading");
                AddLabel(heading, "ДОВОЛЬСТВО", "eyebrow");
                _satisfaction = AddLabel(heading, "", "satisfaction-value");
                VisualElement satisfactionTrack = AddBox(_satisfactionMeter, "satisfaction-track");
                AddBox(satisfactionTrack, "satisfaction-midpoint");
                _satisfactionPointer = AddBox(satisfactionTrack, "satisfaction-pointer");
            }
            _satisfaction.name = "satisfaction";
            _satisfactionPointer.name = "satisfaction-pointer";

            VisualElement stage = AddBox(root, "stage");
            VisualElement controls;
            if (_artMode)
            {
                var capacity = AddBox(header, "art-capacity");
                (_weight, _weightFill) = CreateMeter(AddBox(capacity, "art-weight"), "capacity-weight");
                (_space, _spaceFill) = CreateMeter(AddBox(capacity, "art-space"), "capacity-space");
                ApplyDisplayFont(_weight);
                ApplyDisplayFont(_space);
                header.Add(_satisfactionMeter);
                var board = CreateArtBoard(stage, artwork, header, _satisfactionMeter);
                controls = board.Rail;
                _artLiftMarker = AddLabel(board.Picture, "", "art-lift-marker");
                _artLiftMarker.name = "art-lift-marker";
                _artLiftMarker.pickingMode = PickingMode.Ignore;
                if (scenePreview)
                    _artLiftMarker.style.display = DisplayStyle.None;
            }
            else
            {
                VisualElement building = AddBox(stage, "building");
                VisualElement buildingTitle = AddBox(building, "building-title");
                AddLabel(buildingTitle, "ПЛОЩАДКИ", "eyebrow");
                AddLabel(buildingTitle, "ЛИФТ", "eyebrow");
                VisualElement floors = AddBox(building, "floors");
                for (int floor = ShiftRules.FloorCount; floor >= 1; floor--)
                    CreateFloor(floors, floor);

                _cabin = AddBox(floors, "cabin");
                controls = AddBox(stage, "controls");
                var capacity = AddBox(controls, "capacity");
                (_weight, _weightFill) = CreateMeter(capacity, "capacity-weight");
                (_space, _spaceFill) = CreateMeter(capacity, "capacity-space");
            }
            if (!_artMode)
            {
                _cabin.pickingMode = PickingMode.Ignore;
                _cabinContents = AddLabel(_cabin, "", "cabin-contents");
                _cabinRoutes = AddLabel(_cabin, "", "cabin-routes");
                _cabinRoutes.name = "cabin-routes";
            }
            if (!_artMode)
                AddLabel(controls, "ОСТАНОВКИ", "eyebrow");
            for (int floor = ShiftRules.FloorCount; floor >= 1; floor--)
            {
                CreateControl(controls, floor, selectFloor);
                if (_artMode)
                    CreateArtFloorInfo(_buttons[floor - 1], floor);
            }

            _shiftHint = AddLabel(root, ControlsHint, "shift-hint");
            _shiftHint.name = "shift-hint";

            VisualElement footer = AddBox(root, "footer");
            if (_artMode)
                footer.Add(_phase);
            _summary = AddLabel(footer, "", "summary");
            _status = AddLabel(_artMode ? footer : controls, "", "status");
            _status.name = "lift-status";
            _pause = new Button(togglePause) { name = "pause-shift", text = "Пауза · Esc" };
            _pause.AddToClassList("pause-button");
            _pause.SetEnabled(false);
            footer.Add(_pause);

            _overlay = AddBox(root, "overlay");
            VisualElement card = AddBox(_overlay, "card");
            _overlayTitle = AddLabel(card, "", "card-title");
            _overlayBody = AddLabel(card, "", "card-body");
            _start = new Button(start) { name = "start-shift" };
            _start.AddToClassList("start");
            card.Add(_start);
            if (returnToMenu != null)
            {
                var menu = new Button(returnToMenu) { name = "return-to-menu", text = "Главное меню" };
                menu.AddToClassList("start");
                menu.AddToClassList("return-to-menu");
                card.Add(menu);
            }

            _pauseOverlay = AddBox(root, "overlay");
            _pauseOverlay.name = "shift-pause";
            _pauseOverlay.style.display = DisplayStyle.None;
            VisualElement pauseCard = AddBox(_pauseOverlay, "card");
            pauseCard.AddToClassList("pause-card");
            AddLabel(pauseCard, "ПАУЗА", "pause-title");
            _pauseVolume = new VolumeControl("pause-volume");
            pauseCard.Add(_pauseVolume);
            _resume = new Button(togglePause) { name = "resume-shift", text = "Продолжить" };
            _resume.AddToClassList("pause-button");
            _resume.AddToClassList("pause-resume");
            pauseCard.Add(_resume);
        }

        public void PrepareShiftHints(IReadOnlyList<PassengerCall> schedule)
        {
            _eventHints = schedule
                .Where(call => call.Kind != PassengerKind.Resident)
                .GroupBy(call => call.Kind)
                .Select(group =>
                {
                    PassengerCall first = group.First();
                    int lead = first.Kind == PassengerKind.PartyGuest ? ShiftRules.PartyNoticeTicks : ShiftRules.ApproachTicks;
                    string tip = first.Kind switch
                    {
                        PassengerKind.Mover => $"Переезд · {first.Origin}-й этаж. Коробки займут {first.SpaceUnits} места, доставка даст {first.Reward} очков.",
                        PassengerKind.PartyGuest => $"Гости расходятся · {first.Origin}-й этаж. Гости выйдут по очереди, каждый займёт одно место.",
                        PassengerKind.Courier => $"Курьер · {first.Origin}-й этаж. Ждёт {first.PatienceTicks / ShiftRules.TicksPerSecond} сек., доставка даст {first.Reward} очков.",
                        _ => ""
                    };
                    return (first.Tick - lead, first.Tick + EventHintTailTicks, tip);
                })
                .ToArray();
        }

        public void SetPaused(bool paused, bool canSelectFloor)
        {
            _pauseOverlay.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
            _pause.SetEnabled(!paused);
            foreach (Button button in _buttons)
                button.SetEnabled(!paused && canSelectFloor);

            if (paused)
                _pauseVolume.Refresh();
            Button focus = paused ? _resume : _pause;
            focus.schedule.Execute(() => focus.Focus());
        }

        public void ShowIntro()
        {
            _overlayTitle.text = "Первая смена";
            _overlayBody.text = "Выбирай остановку мышью или клавишами 1–5.\nЭтаж назначения зажжётся после посадки.\nВыбирай этаж с учётом веса, места и награды.\nКабина берёт максимум людей, которые помещаются.";
            _start.text = "Начать смену";
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void HideOverlay() => _overlay.style.display = DisplayStyle.None;

        public void ShowResults(ShiftModel model, int best)
        {
            _overlayTitle.text = "Смена окончена";
            _overlayBody.text = $"Счёт {model.Score}   ·   Рекорд {best}\nДоставлено {model.Delivered}   ·   По лестнице {model.LeftByStairs}\nДовольство {model.Satisfaction}%";
            _start.text = "Повторить смену";
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void Refresh(ShiftModel model, bool active, int? best = null)
        {
            int secondsLeft = (ShiftRules.ShiftTicks - model.Tick + ShiftRules.TicksPerSecond - 1) / ShiftRules.TicksPerSecond;
            _clock.text = $"{secondsLeft / 60:00}:{secondsLeft % 60:00}";
            TrafficPhase phase = ShiftRules.PhaseAt(model.Tick);
            _phase.text = model.Finished ? "Смена окончена" : PhaseName(phase);
            _phase.EnableInClassList("busy", !model.Finished && phase == TrafficPhase.Building);
            _phase.EnableInClassList("peak", !model.Finished && phase >= TrafficPhase.Rush);
            _shiftProgress.style.width = Length.Percent((float)model.Tick / ShiftRules.ShiftTicks * 100);
            _score.text = model.Score.ToString();
            if (_artMode)
                _record.text = (best ?? 0).ToString();
            UpdateSatisfaction(model.Satisfaction);
            if (!_artMode)
            {
                _cabinContents.text = model.OnBoardCount == 0 ? "Пусто" : $"{model.OnBoardCount} чел.";
                _cabinRoutes.text = CabinDestinations(model);
                _cabin.EnableInClassList("doors-open", model.State == LiftState.DoorsOpen && !model.Finished);
            }
            string weight = _artMode ? "ВЕС" : "Вес";
            string space = _artMode ? "МЕСТО" : "Место";
            SetMeter(_weight, _weightFill, $"{weight} {model.WeightKg} / {ShiftRules.MaximumWeightKg} кг", model.WeightKg, ShiftRules.MaximumWeightKg);
            SetMeter(_space, _spaceFill, $"{space} {model.UsedSpace} / {ShiftRules.MaximumSpaceUnits}", model.UsedSpace, ShiftRules.MaximumSpaceUnits);
            _summary.text = $"Доставлено {model.Delivered}   ·   По лестнице {model.LeftByStairs}";
            _status.text = LiftStatus(model, active);
            _pause.SetEnabled(active);
            RefreshShiftHint(model.Tick, active);

            for (int floor = 1; floor <= ShiftRules.FloorCount; floor++)
                RefreshFloor(model, floor, active);
        }

        private void RefreshShiftHint(int tick, bool active)
        {
            if (active)
            {
                foreach (var hint in _eventHints)
                {
                    if (tick < hint.From || tick >= hint.Until)
                        continue;

                    _shiftHint.text = hint.Text;
                    _shiftHint.AddToClassList("hint-event");
                    return;
                }
            }

            _shiftHint.text = ControlsHint;
            _shiftHint.RemoveFromClassList("hint-event");
        }

        public void PositionCabin(double floor, bool moving = false, bool goingUp = false)
        {
            if (!_artMode)
            {
                _cabin.style.top = Length.Percent(10f + (ShiftRules.FloorCount - (float)floor) * 20f);
                return;
            }

            _artLiftMarker.style.top = Length.Percent((22f + (ShiftRules.FloorCount - (float)floor) * 45f) / 223f * 100f);
            _artLiftMarker.EnableInClassList("parked", !moving);
            _artLiftMarker.text = moving ? (goingUp ? "▲" : "▼") : "";
        }

        private void UpdateSatisfaction(int value)
        {
            if (_lastSatisfaction == value)
                return;
            _satisfaction.text = $"{value}%";
            _satisfactionPointer.style.left = Length.Percent(value);
            _satisfactionMeter.EnableInClassList("below-neutral", value < ShiftRules.InitialSatisfaction);
            if (_artMode)
            {
                int mood = value < 35 ? 0 : value >= 65 ? 2 : 1;
                _satisfactionPointer.style.backgroundImage = _satisfactionFaces[mood];
                if (_lastSatisfaction >= 0)
                {
                    _satisfactionPointer.style.scale = new Scale(Vector3.one * 1.22f);
                    _settleSatisfactionFace.ExecuteLater(155);
                }
            }
            _lastSatisfaction = value;
        }

        private static (VisualElement Picture, VisualElement Rail) CreateArtBoard(
            VisualElement stage, RenderTexture artwork, VisualElement header, VisualElement satisfaction)
        {
            VisualElement board = AddBox(stage, "art-board");
            board.name = "art-board";
            VisualElement picture = AddBox(board, "art-viewport");
            picture.name = "art-viewport";
            picture.pickingMode = PickingMode.Ignore;
            VisualElement controls = AddBox(board, "art-controls");
            if (artwork == null)
                return (picture, controls);

            picture.style.backgroundImage = Background.FromRenderTexture(artwork);
            float ratio = (float)artwork.width / artwork.height;
            stage.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                Rect area = evt.newRect;
                const float gap = 8;
                float railWidth = Mathf.Clamp(area.width - area.height * ratio - gap, area.width * 0.2f, area.width * 0.3f);
                float width = Mathf.Max(0, area.width - railWidth - gap);
                board.style.width = area.width;
                board.style.height = area.height;
                board.style.left = 0;
                board.style.top = 0;
                picture.style.width = width;
                controls.style.width = railWidth;
                header.style.width = area.width;
                satisfaction.style.width = railWidth;
            });
            return (picture, controls);
        }

        private void CreateArtFloorInfo(Button button, int floor)
        {
            int index = floor - 1;
            button.AddToClassList("art-floor-button");
            if (floor == 1)
                button.AddToClassList("art-floor-button-last");
            var detail = AddBox(button, "art-floor-info");
            detail.pickingMode = PickingMode.Ignore;
            _riderPortraits[index] = AddBox(detail, "art-rider-portraits");
            _riderPortraits[index].name = $"riders-{floor}";
            _eventNotices[index] = AddLabel(detail, "", "floor-event");
            _eventNotices[index].name = $"floor-event-{floor}";
            var header = AddBox(detail, "waiting-header");
            _queue[index] = AddLabel(header, "", "queue");
            _queue[index].name = $"waiting-{floor}";
            _transfers[index] = AddLabel(header, "", "transfer");
            _stairs[index] = AddLabel(detail, "", "stair-label");
            _waitingPeople[index] = AddBox(detail, "waiting-people");
            _waitingPeople[index].name = $"waiting-people-{floor}";
        }

        private void CreateFloor(VisualElement floors, int floor)
        {
            int index = floor - 1;
            VisualElement row = AddBox(floors, "floor");
            row.name = $"floor-{floor}";
            AddLabel(row, floor.ToString(), "floor-number");

            VisualElement walking = AddBox(row, "walking");
            AddLabel(walking, "≡", "stair-icon");
            _stairs[index] = AddLabel(walking, "ЛЕСТНИЦА", "stair-label");

            VisualElement hallway = AddBox(row, "hallway");
            AddBox(hallway, "apartment-door");
            _approaching[index] = AddBox(hallway, "approach-figure");
            _approaching[index].name = $"approach-{floor}";
            AddBox(_approaching[index], "person-head");
            AddBox(_approaching[index], "person-body");

            VisualElement waiting = AddBox(row, "waiting");
            _eventNotices[index] = AddLabel(waiting, "", "floor-event");
            _eventNotices[index].name = $"floor-event-{floor}";
            VisualElement waitingHeader = AddBox(waiting, "waiting-header");
            _transfers[index] = AddLabel(waitingHeader, "", "transfer");
            _queue[index] = AddLabel(waitingHeader, "", "queue");
            _queue[index].name = $"waiting-{floor}";
            _waitingPeople[index] = AddBox(waiting, "waiting-people");
            _waitingPeople[index].name = $"waiting-people-{floor}";
            AddBox(row, "shaft");
        }

        private void CreateControl(VisualElement parent, int floor, Action<int> selectFloor)
        {
            int index = floor - 1;
            var button = new Button(() => selectFloor(floor)) { name = $"select-{floor}" };
            button.AddToClassList("floor-button");
            ApplyDisplayFont(AddLabel(button, floor.ToString(), "button-number"));
            if (!_artMode)
                _floorStates[index] = AddLabel(button, "", "button-state");
            parent.Add(button);
            _buttons[index] = button;
        }

        private void RefreshFloor(ShiftModel model, int floor, bool active)
        {
            int index = floor - 1;
            int waiting = model.WaitingAt(floor);
            int riding = model.RidingTo(floor);
            bool hasApproach = model.TryGetApproach(floor, out var approaching, out double approach);
            if (!_artMode)
            {
                _approaching[index].style.display = hasApproach ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasApproach)
                    _approaching[index].style.left = Length.Percent(11 + (float)approach * 77);
            }

            _queue[index].text = waiting > 0 ? $"Ждут {waiting}" : hasApproach ? "Подходит" : "Свободно";
            RefreshEventNotice(model, floor, waiting, approaching.Kind);
            RefreshWaitingCards(model, floor, waiting);
            var transfer = model.TransferAt(floor);
            bool recentTransfer = transfer.Tick >= 0 && model.Tick - transfer.Tick < 18;
            int reward = 0;
            if (recentTransfer && transfer.Delivered > 0)
            {
                foreach (var rider in model.AlightingsAt(floor))
                    if (rider.Tick == transfer.Tick)
                        reward += rider.Passenger.Reward;
            }
            _transfers[index].text = recentTransfer
                ? TransferText(transfer.Boarded, transfer.Delivered, reward)
                : "";
            _transfers[index].EnableInClassList("rewarded", reward > 0);
            _queue[index].EnableInClassList("has-call", waiting > 0);
            int lastDeparture = model.LastDepartureAt(floor);
            bool leaving = lastDeparture >= 0 && model.Tick - lastDeparture < 18;
            _stairs[index].EnableInClassList("leaving", leaving);
            _stairs[index].text = leaving ? "УХОД ПО ЛЕСТНИЦЕ" : "ЛЕСТНИЦА";
            if (_artMode)
                _stairs[index].style.display = leaving ? DisplayStyle.Flex : DisplayStyle.None;

            bool selected = active && model.State != LiftState.Idle && model.TargetFloor == floor;
            Button button = _buttons[index];
            button.SetEnabled(active && model.State == LiftState.Idle);
            button.EnableInClassList("called", waiting > 0);
            button.EnableInClassList("destination", riding > 0);
            button.EnableInClassList("selected", selected);
            if (_artMode)
            {
                RefreshRiderPortraits(model, floor);
                _queue[index].style.display = waiting > 0 || hasApproach ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else if (riding > 0 && waiting > 0)
                _floorStates[index].text = $"Едут {riding}  ·  Ждут {waiting}";
            else if (riding > 0)
                _floorStates[index].text = $"Едут {riding}";
            else
                _floorStates[index].text = waiting > 0 ? $"Ждут {waiting}" : "";
        }

        private void RefreshEventNotice(ShiftModel model, int floor, int waiting, PassengerKind kind)
        {
            if (model.PartyApproachingAt(floor))
                kind = PassengerKind.PartyGuest;
            for (int i = 0; kind == PassengerKind.Resident && i < waiting; i++)
                kind = model.WaitingPassengerAt(floor, i).Kind;

            var notice = _eventNotices[floor - 1];
            notice.text = model.Finished ? "" : kind switch
            {
                PassengerKind.Mover => "Переезд",
                PassengerKind.PartyGuest => "Гости расходятся",
                PassengerKind.Courier => $"Курьер · ждёт {ShiftRules.CourierPatienceTicks / ShiftRules.TicksPerSecond} с",
                _ => ""
            };
            notice.style.display = notice.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshRiderPortraits(ShiftModel model, int floor)
        {
            VisualElement row = _riderPortraits[floor - 1];
            int slot = 0;
            for (int i = 0; i < model.OnBoardCount; i++)
            {
                PassengerCall passenger = model.RidingPassengerAt(i);
                if (passenger.Destination != floor)
                    continue;
                if (slot == row.childCount)
                {
                    var image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.AddToClassList("art-rider-face");
                    row.Add(image);
                }
                ((Image)row[slot++]).sprite = _portrait(passenger);
            }
            while (row.childCount > slot)
                row.RemoveAt(row.childCount - 1);
            row.style.display = slot > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyDisplayFont(VisualElement element)
        {
            if (_displayFont != null)
                element.style.unityFontDefinition = FontDefinition.FromFont(_displayFont);
        }

        private void RefreshWaitingCards(ShiftModel model, int floor, int count)
        {
            VisualElement cards = _waitingPeople[floor - 1];
            Span<int> indices = stackalloc int[ElevatorArtwork.MaxVisibleInQueue];
            int shown = _artMode ? ElevatorArtwork.GetVisibleWaitingIndices(model, floor, indices) : count;
            int overflow = count - shown;
            int labels = shown + (overflow > 0 ? 1 : 0);
            while (cards.childCount > labels)
                cards.RemoveAt(cards.childCount - 1);
            while (cards.childCount < labels)
                cards.Add(new WaitingCard(showPatience: !_artMode));

            for (int index = 0; index < shown; index++)
            {
                var card = (WaitingCard)cards[index];
                int queueIndex = _artMode ? indices[index] : index;
                PassengerCall passenger = model.WaitingPassengerAt(floor, queueIndex);
                string eventLabel = passenger.Kind switch
                {
                    PassengerKind.Mover => "Коробки · ",
                    PassengerKind.PartyGuest => "Гость · ",
                    PassengerKind.Courier => "Курьер · ",
                    _ => ""
                };
                card.Load.text = $"{eventLabel}{passenger.WeightKg} кг · {passenger.SpaceUnits} м.";
                card.Reward.text = $"+{passenger.Reward} очк.";
                card.Reward.style.display = DisplayStyle.Flex;
                if (card.Patience != null)
                {
                    float remaining = (float)model.PatienceRemainingAt(floor, queueIndex) / passenger.PatienceTicks;
                    card.Patience.style.width = Length.Percent(remaining * 100);
                    card.Patience.EnableInClassList("urgent", remaining <= 0.25f);
                    card.Patience.EnableInClassList("warning", remaining <= 0.5f);
                }
            }
            if (overflow > 0)
            {
                var extra = (WaitingCard)cards[shown];
                extra.Load.text = $"+{overflow} в очереди";
                extra.Reward.style.display = DisplayStyle.None;
            }
        }

        private static string TransferText(int boarded, int delivered, int reward)
        {
            string entry = boarded == 1 ? "Сел 1" : $"Сели {boarded}";
            string exit = delivered == 1 ? "Вышел 1" : $"Вышли {delivered}";
            if (reward > 0)
                exit += $" · +{reward} очк.";
            if (boarded == 0)
                return exit;

            return delivered == 0 ? entry : $"{entry} · {exit}";
        }

        private static string CabinDestinations(ShiftModel model)
        {
            string destinations = "";
            for (int floor = 1; floor <= ShiftRules.FloorCount; floor++)
            {
                int count = model.RidingTo(floor);
                if (count == 0)
                    continue;

                string stop = count == 1 ? floor.ToString() : $"{floor}×{count}";
                destinations += destinations.Length == 0 ? stop : $" · {stop}";
            }

            return destinations.Length == 0 ? "—" : $"→ {destinations}";
        }

        private static (Label Label, VisualElement Fill) CreateMeter(VisualElement parent, string name)
        {
            Label label = AddLabel(parent, "", "capacity-label");
            label.name = name;
            VisualElement track = AddBox(parent, "capacity-track");
            return (label, AddBox(track, "capacity-fill"));
        }

        private static void SetMeter(Label label, VisualElement fill, string value, int used, int limit)
        {
            label.text = value;
            fill.style.width = Length.Percent(100f * used / limit);
            fill.EnableInClassList("full", used == limit);
        }

        private static string PhaseName(TrafficPhase phase)
        {
            return phase switch
            {
                TrafficPhase.Quiet => "ТИХОЕ НАЧАЛО",
                TrafficPhase.Building => "ПОТОК РАСТЁТ",
                TrafficPhase.Rush => "ЧАС ПИК",
                TrafficPhase.FinalRush => "ФИНАЛЬНЫЙ НАПЛЫВ",
                _ => ""
            };
        }

        private static string LiftStatus(ShiftModel model, bool active)
        {
            if (!active)
                return "Лифт остановлен";
            if (model.State == LiftState.Moving)
                return $"Едет на {model.TargetFloor}-й этаж";
            if (model.State == LiftState.DoorsOpen)
                return "Двери открыты";

            return "Выбери этаж";
        }

        private static VisualElement AddBox(VisualElement parent, string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            parent.Add(element);
            return element;
        }

        private static Label AddLabel(VisualElement parent, string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }
    }
}

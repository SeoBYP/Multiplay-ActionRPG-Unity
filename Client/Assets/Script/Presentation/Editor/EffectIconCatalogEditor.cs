using Game.Presentation.InGame;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Editor
{
    /// <summary>
    /// EffectIconCatalog용 UI Toolkit 커스텀 인스펙터.
    ///   - 색상(버프/디버프) 편집
    ///   - 카테고리 → Sprite 매핑 리스트 (행마다 아이콘을 버프/디버프 색으로 미리보기)
    /// </summary>
    [CustomEditor(typeof(EffectIconCatalog))]
    public sealed class EffectIconCatalogEditor : UnityEditor.Editor
    {
        private const float PreviewSize = 34f;

        public override VisualElement CreateInspectorGUI()
        {
            var so = serializedObject;
            var root = new VisualElement { style = { marginTop = 4 } };

            root.Add(MakeTitle("Effect Icon Catalog"));
            root.Add(new HelpBox(
                "버프/디버프 HUD 표시 매핑(표시 전용). 같은 카테고리는 같은 아이콘을 쓰고, 색은 버프/디버프(polarity)로 구분합니다.",
                HelpBoxMessageType.Info));

            // ── 색상 ──────────────────────────────
            var colorSection = MakeSection("색상 (Polarity)");
            colorSection.Add(new PropertyField(so.FindProperty("buffColor"), "버프 색 (증가)"));
            colorSection.Add(new PropertyField(so.FindProperty("debuffColor"), "디버프 색 (감소)"));
            root.Add(colorSection);

            // ── 아이콘 매핑 ────────────────────────
            var iconSection = MakeSection("아이콘 (카테고리 → Sprite)");
            var listContainer = new VisualElement();
            iconSection.Add(listContainer);

            var addButton = new Button(() => { AddEntry(); RebuildList(listContainer); })
            {
                text = "＋ 카테고리 추가",
                style = { marginTop = 6, alignSelf = Align.FlexStart }
            };
            iconSection.Add(addButton);
            root.Add(iconSection);

            RebuildList(listContainer);

            // 색 변경 시 미리보기 갱신
            root.TrackPropertyValue(so.FindProperty("buffColor"), _ => RebuildList(listContainer));
            root.TrackPropertyValue(so.FindProperty("debuffColor"), _ => RebuildList(listContainer));

            root.Bind(so);
            return root;
        }

        // ── 리스트 ──────────────────────────────

        private void RebuildList(VisualElement container)
        {
            container.Clear();
            serializedObject.Update();

            var entries = serializedObject.FindProperty("entries");
            Color buff = serializedObject.FindProperty("buffColor").colorValue;
            Color debuff = serializedObject.FindProperty("debuffColor").colorValue;

            if (entries.arraySize == 0)
            {
                container.Add(new HelpBox("등록된 카테고리가 없습니다. 아래 버튼으로 추가하세요.", HelpBoxMessageType.None));
                return;
            }

            for (int i = 0; i < entries.arraySize; i++)
            {
                int index = i;
                var element = entries.GetArrayElementAtIndex(i);
                var iconProp = element.FindPropertyRelative("icon");
                container.Add(MakeRow(element, iconProp, index, buff, debuff, container));
            }

            container.Bind(serializedObject);
        }

        private VisualElement MakeRow(SerializedProperty element, SerializedProperty iconProp,
            int index, Color buff, Color debuff, VisualElement listContainer)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4,
                    paddingTop = 2, paddingBottom = 2, paddingLeft = 4, paddingRight = 4,
                    backgroundColor = new Color(1f, 1f, 1f, 0.03f),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                }
            };

            var sprite = iconProp.objectReferenceValue as Sprite;
            var buffPreview = MakePreview(sprite, buff, "버프");
            var debuffPreview = MakePreview(sprite, debuff, "디버프");

            var category = new PropertyField(element.FindPropertyRelative("category"), string.Empty)
            {
                style = { flexGrow = 1, marginRight = 6 }
            };
            var icon = new PropertyField(iconProp, string.Empty)
            {
                style = { width = 170, marginRight = 6 }
            };

            // 아이콘 교체 시 두 미리보기 동기화
            row.TrackPropertyValue(iconProp, p =>
            {
                var s = p.objectReferenceValue as Sprite;
                ((Image)buffPreview[0]).sprite = s;
                ((Image)debuffPreview[0]).sprite = s;
            });

            var remove = new Button(() => { RemoveEntry(index); RebuildList(listContainer); })
            {
                text = "✕",
                tooltip = "이 카테고리 제거",
                style = { width = 24, marginLeft = 4 }
            };

            row.Add(category);
            row.Add(icon);
            row.Add(buffPreview);
            row.Add(debuffPreview);
            row.Add(remove);
            return row;
        }

        private static VisualElement MakePreview(Sprite sprite, Color tint, string label)
        {
            var box = new VisualElement
            {
                style = { alignItems = Align.Center, marginLeft = 4, marginRight = 4 }
            };
            var image = new Image
            {
                sprite = sprite,
                tintColor = tint,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = PreviewSize, height = PreviewSize }
            };
            var caption = new Label(label)
            {
                style = { fontSize = 9, unityTextAlign = TextAnchor.MiddleCenter, color = new Color(0.7f, 0.7f, 0.7f) }
            };
            box.Add(image);
            box.Add(caption);
            return box;
        }

        // ── 배열 조작 ────────────────────────────

        private void AddEntry()
        {
            serializedObject.Update();
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize++;
            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveEntry(int index)
        {
            serializedObject.Update();
            var entries = serializedObject.FindProperty("entries");
            if (index >= 0 && index < entries.arraySize)
                entries.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }

        // ── 스타일 헬퍼 ──────────────────────────

        private static Label MakeTitle(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    marginBottom = 6,
                }
            };
        }

        private static VisualElement MakeSection(string title)
        {
            var section = new VisualElement
            {
                style =
                {
                    marginTop = 8,
                    paddingTop = 6, paddingBottom = 8, paddingLeft = 8, paddingRight = 8,
                    backgroundColor = new Color(0f, 0f, 0f, 0.12f),
                    borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                }
            };
            section.Add(new Label(title)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 }
            });
            return section;
        }
    }
}

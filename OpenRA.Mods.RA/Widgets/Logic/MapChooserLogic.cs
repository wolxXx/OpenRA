#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
	public class MapChooserLogic
	{
		Map map;
		Widget scrollpanel;
		ScrollItemWidget itemTemplate;

		[ObjectCreator.UseCtor]
		internal MapChooserLogic([ObjectCreator.Param] Widget widget,
			[ObjectCreator.Param] string initialMap,
			[ObjectCreator.Param] Action onExit,
			[ObjectCreator.Param] Action<Map> onSelect)
		{
			map = Game.modData.AvailableMaps[WidgetUtils.ChooseInitialMap(initialMap)];

			widget.GetWidget<MapPreviewWidget>("MAP_PREVIEW").Map = () => map;
			widget.GetWidget<LabelWidget>("CURMAP_TITLE").GetText = () => map.Title;
			widget.GetWidget<LabelWidget>("CURMAP_AUTHOR").GetText = () => map.Author;
			widget.GetWidget<LabelWidget>("CURMAP_DESC").GetText = () => map.Description;
			widget.GetWidget<LabelWidget>("CURMAP_DESC_LABEL").IsVisible = () => map.Description != null;
			widget.GetWidget<LabelWidget>("CURMAP_SIZE").GetText = () => "{0}x{1}".F(map.Bounds.Width, map.Bounds.Height);
			widget.GetWidget<LabelWidget>("CURMAP_THEATER").GetText = () => Rules.TileSets[map.Tileset].Name;
			widget.GetWidget<LabelWidget>("CURMAP_PLAYERS").GetText = () => map.PlayerCount.ToString();

			widget.GetWidget<ButtonWidget>("BUTTON_OK").OnClick = () => { Widget.CloseWindow(); onSelect(map); };
			widget.GetWidget<ButtonWidget>("BUTTON_CANCEL").OnClick = () => { Widget.CloseWindow(); onExit(); };

			scrollpanel = widget.GetWidget<ScrollPanelWidget>("MAP_LIST");
			itemTemplate = scrollpanel.GetWidget<ScrollItemWidget>("MAP_TEMPLATE");
			EnumerateMaps();
		}

		void EnumerateMaps()
		{
			scrollpanel.RemoveChildren();

			var maps = Game.modData.AvailableMaps
				.Where(kv => kv.Value.Selectable)
				.OrderBy(kv => kv.Value.PlayerCount)
				.ThenBy(kv => kv.Value.Title);

			foreach (var kv in maps)
			{
				var m = kv.Value;
				var item = ScrollItemWidget.Setup(itemTemplate, () => m == map, () => map = m);
				item.GetWidget<LabelWidget>("TITLE").GetText = () => m.Title;
				item.GetWidget<LabelWidget>("PLAYERS").GetText = () => "{0}".F(m.PlayerCount);
				item.GetWidget<LabelWidget>("TYPE").GetText = () => m.Type;
				scrollpanel.AddChild(item);
			}
		}
	}
}

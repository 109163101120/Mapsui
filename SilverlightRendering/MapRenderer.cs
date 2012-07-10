﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using SharpMap;
using SharpMap.Geometries;
using SharpMap.Layers;
using SharpMap.Rendering;
using SharpMap.Styles;
using SharpMap.Styles.Thematics;

namespace SilverlightRendering
{
    public class MapRenderer : IRenderer
    {
        private readonly Canvas target;

        public MapRenderer()
        {
            target = new Canvas();
        }

        public MapRenderer(Canvas target)
        {
            this.target = target;
        }

        public void Render(IView view, IEnumerable<ILayer> layers)
        {
            foreach (var child in target.Children)
            {
                if (child is Canvas)
                {
                    (child as Canvas).Children.Clear();
                }
            }
            target.Children.Clear();
                        
            foreach (var layer in layers)
            {
                if (layer.Enabled &&
                    layer.MinVisible <= view.Resolution &&
                    layer.MaxVisible >= view.Resolution)
                {
                    RenderLayer(target, view, layer);
                }
            }
            target.Arrange(new Rect(0, 0, view.Width, view.Height));
        }

        private static void RenderLayer(Canvas target, IView view, ILayer layer)
        {
            if (layer.Enabled == false) return;

            if (layer is LabelLayer)
            {
                var labelLayer = layer as LabelLayer;
                if (labelLayer.UseLabelStacking)
                {
                    target.Children.Add(LabelRenderer.RenderStackedLabelLayer(view, labelLayer));
                }
                else
                {
                    target.Children.Add(LabelRenderer.RenderLabelLayer(view, labelLayer));
                }
            }
            else
            {
                target.Children.Add(RenderVectorLayer(view, layer));
            }
        }

        private static Canvas RenderVectorLayer(IView view, ILayer layer)
        {
            var canvas = new Canvas();
            canvas.Opacity = layer.Opacity;

            var features = layer.GetFeaturesInView(view.Extent, view.Resolution).ToList();

            foreach (var layerStyle in layer.Styles)
            {
                var style = layerStyle; // This is the default that could be overridden by an IThemeStyle

                foreach (var feature in features)
                {
                    if (layerStyle is IThemeStyle) style = (layerStyle as IThemeStyle).GetStyle(feature);
                    if ((style == null) || (style.Enabled == false) || (style.MinVisible > view.Resolution) || (style.MaxVisible < view.Resolution)) continue;

                    RenderFeature(canvas, view, style, feature);
                }
            }

            foreach (var feature in features)
            {
                var styles = feature.Styles;
                foreach (var style in styles)
                {
                    if (feature.Styles != null && style.Enabled)
                    {
                        RenderFeature(canvas, view, style, feature);
                    }
                }
            }

            return canvas;
        }

        private static void RenderFeature(Canvas canvas, IView view, IStyle style, SharpMap.Providers.IFeature feature)
        {
            if (style is LabelStyle)
            {
                canvas.Children.Add(LabelRenderer.RenderLabel(feature.Geometry.GetBoundingBox().GetCentroid(), new Offset(), style as LabelStyle, view));
            }
            else 
            {
                var renderedGeometry = feature.RenderedGeometry.ContainsKey(style) ? feature.RenderedGeometry[style] as UIElement : null;
                if (renderedGeometry == null) 
                {
                    renderedGeometry = RenderGeometry(canvas, view, style, feature);
                    if (feature.Geometry is SharpMap.Geometries.Point || feature.Geometry is IRaster) // positioning only supported for point and raster
                        feature.RenderedGeometry[style] = renderedGeometry;
                }
                else
                {
                    PositionGeometry(renderedGeometry, view, style, feature);
                }
                canvas.Children.Add(renderedGeometry);
            }
        }

        private static UIElement RenderGeometry(Canvas canvas, IView view, IStyle style, SharpMap.Providers.IFeature feature)
        {
            if (feature.Geometry is SharpMap.Geometries.Point)
                return GeometryRenderer.RenderPoint(feature.Geometry as SharpMap.Geometries.Point, style, view);
            if (feature.Geometry is MultiPoint)
                return GeometryRenderer.RenderMultiPoint(feature.Geometry as MultiPoint, style, view);
            if (feature.Geometry is LineString)
                return GeometryRenderer.RenderLineString(feature.Geometry as LineString, style, view);
            if (feature.Geometry is MultiLineString)
                return GeometryRenderer.RenderMultiLineString(feature.Geometry as MultiLineString, style, view);
            if (feature.Geometry is Polygon)
                return GeometryRenderer.RenderPolygon(feature.Geometry as Polygon, style, view);
            if (feature.Geometry is MultiPolygon)
                return GeometryRenderer.RenderMultiPolygon(feature.Geometry as MultiPolygon, style, view);
            if (feature.Geometry is IRaster)
                return GeometryRenderer.RenderRaster(feature.Geometry as IRaster, style, view);
            return null;
        }

        private static void PositionGeometry(UIElement renderedGeometry, IView view, IStyle style, SharpMap.Providers.IFeature feature)
        {
            if (feature.Geometry is SharpMap.Geometries.Point)
                GeometryRenderer.PositionPoint(renderedGeometry, feature.Geometry as SharpMap.Geometries.Point, style, view);
            if (feature.Geometry is MultiPoint)
                return;
            if (feature.Geometry is LineString)
                return;
            if (feature.Geometry is MultiLineString)
                return;
            if (feature.Geometry is Polygon)
                return;
            if (feature.Geometry is MultiPolygon)
                return;
            if (feature.Geometry is IRaster)
                GeometryRenderer.PositionRaster(renderedGeometry, feature.Geometry.GetBoundingBox(), view);
        }

        public static void Animate(DependencyObject target, string property, double from, double to, int duration, EventHandler completed)
        {
            var animation = new DoubleAnimation();
            animation.From = from;
            animation.To = to;
            animation.Duration = new TimeSpan(0, 0, 0, 0, duration);
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));

            var storyBoard = new Storyboard();
            storyBoard.Children.Add(animation);
            storyBoard.Completed += completed;
            storyBoard.Begin();
        }

        public Stream ToBitmapStream(double width, double height)
        {
            target.Arrange(new Rect(0, 0, width, height));
#if !SILVERLIGHT
            var renderTargetBitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, new PixelFormat());
            renderTargetBitmap.Render(target);
            var bitmap = new PngBitmapEncoder();
            bitmap.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
            var bitmapStream = new MemoryStream();
            bitmap.Save(bitmapStream);
#else
            var writeableBitmap = new WriteableBitmap((int)width, (int)height);
            writeableBitmap.Render(target, null);
            var bitmapStream = Utilities.ConverToBitmapStream(writeableBitmap);
#endif
            return bitmapStream;
        }
    }
}

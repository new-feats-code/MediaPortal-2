#region Copyright (C) 2007 Team MediaPortal

/*
    Copyright (C) 2007 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion
using System;
using System.Collections.Generic;
using System.Text;
using SkinEngine.Controls.Visuals;
using System.Drawing;
using MediaPortal.Core.Properties;
using Rectangle = System.Drawing.Rectangle;

namespace SkinEngine.Controls.Panels
{
  public class StackPanel : Panel
  {
    Property _orientationProperty;

    /// <summary>
    /// Initializes a new instance of the <see cref="StackPanel"/> class.
    /// </summary>
    public StackPanel()
    {
      Init();
    }
    public StackPanel(StackPanel v)
      : base(v)
    {
      Init();
    }
    void Init()
    {
      _orientationProperty = new Property(Orientation.Vertical);
      _orientationProperty.Attach(new PropertyChangedHandler(OnPropertyInvalidate));
    }
    public override object Clone()
    {
      return new StackPanel(this);
    }

    /// <summary>
    /// Gets or sets the orientation property.
    /// </summary>
    /// <value>The orientation property.</value>
    public Property OrientationProperty
    {
      get
      {
        return _orientationProperty;
      }
      set
      {
        _orientationProperty = value;
      }
    }

    /// <summary>
    /// Gets or sets the orientation.
    /// </summary>
    /// <value>The orientation.</value>
    public Orientation Orientation
    {
      get
      {
        return (Orientation)_orientationProperty.GetValue();
      }
      set
      {
        _orientationProperty.SetValue(value);
      }
    }

    /// <summary>
    /// measures the size in layout required for child elements and determines a size for the FrameworkElement-derived class.
    /// </summary>
    /// <param name="availableSize">The available size that this element can give to child elements.</param>
    public override void Measure(System.Drawing.Size availableSize)
    {
      _desiredSize = new System.Drawing.Size((int)Width, (int)Height);
      if (Width == 0)
        _desiredSize.Width = (int)availableSize.Width - (int)(Margin.X + Margin.W);
      if (Height == 0)
        _desiredSize.Height = (int)availableSize.Height - (int)(Margin.Y + Margin.Z);

      float totalHeight = 0.0f;
      float totalWidth = 0.0f;
      Size childSize = new Size(_desiredSize.Width, _desiredSize.Height);
      foreach (UIElement child in Children)
      {
        if (!child.IsVisible) continue;
        if (childSize.Width < 0 || childSize.Height < 0)
        {
        }
        child.Measure(childSize);
        if (child.DesiredSize.Width < 0 || child.DesiredSize.Height < 0)
        {
        }
        if (Orientation == Orientation.Vertical)
        {
          childSize.Height -= child.DesiredSize.Height;
          totalHeight += child.DesiredSize.Height;
          if (child.DesiredSize.Width > totalWidth)
            totalWidth = child.DesiredSize.Width;
        }
        else
        {
          childSize.Width -= child.DesiredSize.Width;
          totalWidth += child.DesiredSize.Width;

          if (child.DesiredSize.Height > totalHeight)
            totalHeight = child.DesiredSize.Height;
        }
      }
      if (Width > 0) totalWidth = (float)Width;
      if (Height > 0) totalHeight = (float)Height;
      _desiredSize = new Size((int)totalWidth, (int)totalHeight);
      _desiredSize.Width += (int)(Margin.X + Margin.W);
      _desiredSize.Height += (int)(Margin.Y + Margin.Z);
      base.Measure(availableSize);
    }

    /// <summary>
    /// Arranges the UI element
    /// and positions it in the finalrect
    /// </summary>
    /// <param name="finalRect">The final size that the parent computes for the child element</param>
    public override void Arrange(Rectangle finalRect)
    {
      _availablePoint = new Point(finalRect.Location.X, finalRect.Location.Y);
      Rectangle layoutRect = new Rectangle(finalRect.X, finalRect.Y, finalRect.Width, finalRect.Height);
      layoutRect.X += (int)(Margin.X);
      layoutRect.Y += (int)(Margin.Y);
      layoutRect.Width -= (int)(Margin.X + Margin.W);
      layoutRect.Height -= (int)(Margin.Y + Margin.Z);
      ActualPosition = new Microsoft.DirectX.Vector3(layoutRect.Location.X, layoutRect.Location.Y, 1.0f); ;
      ActualWidth = layoutRect.Width;
      ActualHeight = layoutRect.Height;
      switch (Orientation)
      {
        case Orientation.Vertical:
          {
            float totalHeight = 0;
            foreach (FrameworkElement child in Children)
            {
              if (!child.IsVisible) continue;
              Point location = new Point((int)(this.ActualPosition.X), (int)(this.ActualPosition.Y + totalHeight));
              Size size = new Size(child.DesiredSize.Width, child.DesiredSize.Height);

              //align horizontally 
              if (AlignmentX == AlignmentX.Center)
              {
                location.X += (int)((layoutRect.Width - child.DesiredSize.Width) / 2);
              }
              else if (AlignmentX == AlignmentX.Right)
              {
                location.X = layoutRect.Right - child.DesiredSize.Width;
              }

              child.Arrange(new Rectangle(location, size));
              totalHeight += child.DesiredSize.Height;
            }
          }
          break;

        case Orientation.Horizontal:
          {
            float totalWidth = 0;
            foreach (FrameworkElement child in Children)
            {
              if (!child.IsVisible) continue;
              Point location = new Point((int)(this.ActualPosition.X + totalWidth), (int)(this.ActualPosition.Y));
              Size size = new Size(child.DesiredSize.Width, child.DesiredSize.Height);
              
              //align vertically 
              if (AlignmentY == AlignmentY.Center)
              {
                location.Y += (int)((layoutRect.Height - child.DesiredSize.Height) / 2);
              }
              else if (AlignmentY == AlignmentY.Bottom)
              {
                location.Y += (int)(layoutRect.Height - child.DesiredSize.Height);
              }

              //ArrangeChild(child, ref location);
              child.Arrange(new Rectangle(location, size));
              totalWidth += child.DesiredSize.Width;
            }
          }
          break;
      }
      base.PerformLayout();
      base.Arrange(layoutRect);
    }
  }
}

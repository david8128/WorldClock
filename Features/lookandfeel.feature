Feature: Include settings for icons and make it transparent background

 Scenario:  Given that currently there should exist an option to select the cities to add to the clocks add a settings icon and display a settings window
            And create a button after activating settings mode to delete a given city by clicking that button on the main window
            And the settings window should enable a way to see transparent background  as shown in the image called @transparent blurry effect.png or windows-terminal-acrylic-transparent-background.jpg, transparent means there is a acrilic/transparency mode given the backhand windows displayed below the window, while changing this setting it should show live changes in the app changing the transparency
            And the settings should have a light theme and a dark theme, if possible provide similar color themes as defaults in VSCode Color Themes and common ones like Catppuccin Dark Pro, Catppuccin Noctis Latte, Solarized, One Dark, Monokai, Ariake, Nord Dark and Tokio Night
            And make sure text colors and icons are adjusted to the theme, making icons have a good contrast with the background
            And apply the same look and feel from the main window to the settings window
            And the STD or DST should be visible in all clocks.
            Then test this new window for textboxes for adding cities
            And test the delete city feature
            And test the transparency live changes
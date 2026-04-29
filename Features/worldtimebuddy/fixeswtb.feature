Feature: Fixes on time translator

    Scenario:
        Given that it is hard to get the date for each timezone add the proper label of date for each choosen clock/location and when same selection box the same time window has two different dates use different colors as shown in @images/wtb2.png
        And Transpose the rows to columns and columns to rows in the time translator
        And fix the search given that currently the selection of same time in time translator is per hour/ hour basis and individual, change it to half hour and multiple selection possible, selection should be a time frame or time window only, composed by one or multiple half hour "windows"
# Val2022

This tool downloads the results from the Swedish general election in 2022 (_allmänt val till riksdag,
regionsfullmäktige och kommunfullmäktige i Sverige_) from
the Swedish election authority (_Valmyndigheten_, [https://www.val.se/](https://val.se/)) 
using best practice methods to limit bandwith usage. The downloading
code is relatively stable. Depending on the stability of the authority's API
over time, it should be relatively easy to adopt to future elections.

The tool also includes a highly customized ad-hoc output of 
some aggregated voting data including the preliminary seat 
count (_mandatfördelning_) for the national parliament (_Riksdagen_) during 
both the preliminary and the final counting process (_rösträkning_).

When run from the command-line there are various switches
for modifying data source and aggregation rules.

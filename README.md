smashCache
==========

A string based key value mapping library for windows phone 8+. It allows keys to be persistent and also has a garbage collector to identify and remove old and unused keys when memory threshold set is nearing.

Usage:

set:
smashCache.set("{KEY}", "{VALUE}", [{PERSISTS}]);
return: void;


get: 
smashCache.get("{KEY}");
return: "{VALUE}";

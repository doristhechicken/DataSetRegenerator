# DataSetRegenerator
If you manually edit a .xsd dataset in Visual Studio, the MSDataSetGenerator custom tool regenerates the .designer.cs code for the data objects and TableAdapterManager.

However, if you use an external tool to modify the .xsd, for example, in a pre-build step, VS will not regenerate the .designer.cs file.

This tool does the same job, and can be used as a pre-build step to regenerate the .designer.cs file from the .xsd schema. 

The output is functionally identical to what MSDataSetGenerator does, with some differences in versions and for some strange reason the tableadapter order in the TableAdapterManager. Works for me anyway. If you want to check the output in a diff tool, do a search and replace to correct the version numbers from 4.0.0.0 to 15.0.0.0 or whatever.

cheers

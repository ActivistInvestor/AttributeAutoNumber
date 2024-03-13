# AttributeAutoNumber
Disclaimer:

This code is intended for experimental/demonstration 
purposes only,and as-provided, is not necessarily fit 
or suitable for any specific purpose.

AutoNumberAttributeOverrule:

An ObjectOverrule that automates the assignment of unique, 
incremental numeric values to all newly-created instances 
of an attribute with a given tag, owned by references to a
given block.

With the code loaded and running, it should not be 
possible for a user to create multiple instances of 
the block reference, having identical values in the 
attribute that's managed by the Overrule, including 
by way of operations like INSERT (from block or file), 
DXFIN, etc.

To use the sample, you need a block with at least one
attribute. With at least one insertion of the block in
the current drawing, issue the AUTONUM command, and
select the attribute whose value is to be managed. 

The demonstration uses block attributes mainly for the
purpose of making the behavior easily observable, but 
the same concepts and approach used can be applied to 
data stored in Xdata or Xrecords as well.

When using this approach for ensuring unique values in
xdata or in an extension dictionary, you would use an 
Overrule with an XData filter or Dictionary filter, to
minimize overhead, which may be the only thing that may
make the use of an ObjectOverrule feasable, considering
the high overhead of an unconstrained ObjectOverrule. 

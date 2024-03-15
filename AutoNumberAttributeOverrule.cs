/// AutoNumberAttributeOverrule.cs  2011  Tony Tanzillo
/// Distributed under the terms of the MIT license

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

namespace Autodesk.AutoCAD.DatabaseServices.MyExtensions
{
   /// <summary>
   ///
   /// Disclaimer:
   ///
   /// This code is intended to serve as a proof-of-concept,
   /// for demonstration purposes, and as-provided, is not 
   /// necessarily fit for any specific purpose.
   ///
   /// AutoNumberAttributeOverrule:
   ///
   /// An ObjectOverrule that automates the assignment of unique,
   /// incremental numeric values to all newly-created instances
   /// of an attribute with a given tag, owned by references to a
   /// given block.
   ///
   /// With the code loaded and running, it should not be
   /// possible for a user to create multiple instances of
   /// the block reference, having identical values in the
   /// attribute that's managed by the Overrule, including
   /// by way of operations like INSERT, COPY, etc.
   ///
   /// To use the sample, you need a block with at least one
   /// attribute. With at least one insertion of the block in
   /// the current drawing, issue the AUTONUM command, and
   /// select the attribute whose value is to be managed.
   /// 
   /// After that, try using the INSERT command to insert one
   /// or more insertions of the selected block/attribute, and
   /// you should see the attribute in each newly-inserted
   /// block reference assigned a value that is 1 greater  than
   /// the value in the most-recently inserted block/attribute.
   /// 
   /// Next try using the COPY command, selecting one or more
   /// insertions of the automumber-managed block/attiribute,
   /// resulting in one or more copies of them, and you should 
   /// see that in each newly-created copy of the selected 
   /// block/attribute, the value of the attribute has been 
   /// incremented to be unique across all insertion attributes.
   ///
   /// The demonstration uses block attributes mainly for the
   /// purpose of making the behavior easily observable, but
   /// the same concepts and approach used can be applied to
   /// values stored in Xdata or Xrecords as well.
   ///
   /// When using this approach for ensuring unique values in
   /// xdata or in an extension dictionary, you would use an
   /// Overrule with an XData filter or XDictionary filter, to
   /// minimize overhead.
   /// 
   /// Known issues/caveats:
   /// 
   /// 1. This proof-of-concept does not address several issues
   ///    that would be relevant in real applications, including
   ///    UNDO/REDO (decrementing/incrementing the seed value if 
   ///    creation of the object that caused it to increment were 
   ///    undone/redone).
   ///    
   ///    The UNDO/REDO problem can be solved by using application-
   ///    defined system variables whose value is managed by the
   ///    undo mechansim. If you UNDO changes to a database, any
   ///    changes to the application-defined sysvar will be undone
   ///    as well. So, the 'seed' value that is assigned to each
   ///    instance of the numbered object can be persisted in an
   ///    application-defined system variable, and its value will
   ///    be managed by the undo mechanism.
   ///    
   /// 2. Numbering scope/namespaces: 
   /// 
   ///    Realistic application of this concept would likely 
   ///    require seperate 'namespaces' for auto-incrementing 
   ///    values. For example, blocks in one document would be 
   ///    automatically numbered separately from the same 
   ///    block(s) in another document. The same could apply
   ///    to owner-scoped and block-scoped namespaces, where
   ///    there may be a seperate 'next available number' that
   ///    is maintained for each owner (e.g. BlockTableRecord), 
   ///    or for each Block.
   ///
   /// </summary>

   class AutoNumberAttributeOverrule : ObjectOverrule<AttributeReference>
   {
      string tag = null;
      ObjectId blockId = ObjectId.Null;
      Database db = null;
      int next = 1;

      public AutoNumberAttributeOverrule( ObjectId blockId, string tag )
         : base( false )
      {
         if( string.IsNullOrWhiteSpace( tag ) )
            throw new ArgumentException( "tag" );
         if( blockId.IsNull || !blockId.IsValid )
            throw new ArgumentException( "blockId" );
         this.tag = tag.ToUpper();
         this.db = blockId.Database;
         this.blockId = blockId;
         next = GetSeedValue( this.blockId, this.tag );
      }

      internal int Next
      {
         get
         {
            return next;
         }
         set
         {
            next = Math.Max( value, 1 );
         }
      }

      string GetNextValue()
      {
         return ( next++ ).ToString();
      }

      /// <summary>
      /// Finds the highest existing value across all
      /// attributes with the given tag, in all block
      /// references.
      /// </summary>

      static int GetSeedValue(ObjectId blockId, string tag )
      {
         int result = 0;
         using(Transaction tr = new OpenCloseTransaction())
         {
            // From all insertions of the given block, get the numerically-greatest
            // TextString value of the AttributeReference with the given tag, as an
            // integer:

            BlockTableRecord btr = tr.GetObject<BlockTableRecord>(blockId);
            result = btr.GetAttributeReferences( tr, tag ).Select( Parse ).Max();
            tr.Commit();
         }
         return result + 1;
      }

      static int Parse( AttributeReference attref )
      {
         int num = 1;
         int.TryParse( attref.TextString, out num );
         return num;
      }

      public override void Close( DBObject obj )
      {
         if( obj.Database == this.db && obj.IsNewObject && obj.IsWriteEnabled )
         {
            AttributeReference attref = obj as AttributeReference;
            if( attref != null && attref.Tag.ToUpper() == this.tag )
            {
               attref.TextString = GetNextValue();
            }
         }
         base.Close( obj );
      }

   }

   /// <summary>
   /// Excerpts from AcDbLinq.cs
   /// </summary>

   internal static class AcDbExtensionMethods
   {

      /// <summary>
      /// Opens an object with a transaction, and casts it to the
      /// generic argument.
      /// </summary>

      public static T GetObject<T>( this Transaction tr, ObjectId id, OpenMode mode = OpenMode.ForRead )
         where T : DBObject
      {
         return (T) tr.GetObject(id, mode, false, false);
      }

      /// <summary>
      /// Gets all AttributeReferences with the specified tag, 
      /// from every insertion of the given block.
      /// </summary>

      public static IEnumerable<AttributeReference> GetAttributeReferences( this BlockTableRecord btr, Transaction tr, string tag )
      {
         if( btr == null )
            throw new ArgumentNullException( "btr" );
         if( tr == null )
            throw new InvalidOperationException( "No transaction" );
         string s = tag.ToUpper();
         foreach( var blkref in btr.GetBlockReferences(tr) )
         {
            var attref = blkref.GetAttributes( tr ).FirstOrDefault( a => a.Tag.ToUpper() == s );
            if( attref != null )
               yield return attref;
         }
      }

      /// <summary>
      /// Get AttributeReferences from the given block reference (lazy)
      /// </summary>

      public static IEnumerable<AttributeReference> GetAttributes( this BlockReference blkref, Transaction tr = null, OpenMode mode = OpenMode.ForRead )
      {
         if( blkref == null )
            throw new ArgumentNullException( "blkref" );
         if( tr == null )
            throw new InvalidOperationException( "No transaction" );
         foreach( ObjectId id in blkref.AttributeCollection )
            yield return tr.GetObject<AttributeReference>( id, mode );
      }

      /// <summary>
      /// Get all references to the given BlockTableRecord,
      /// including anonymous dynamic block references.
      /// </summary>

      public static IEnumerable<BlockReference> GetBlockReferences( this BlockTableRecord btr, Transaction tr, OpenMode mode = OpenMode.ForRead, bool directOnly = true )
      {
         if( btr == null )
            throw new ArgumentNullException( "btr" );
         if( tr == null )
            throw new InvalidOperationException( "No transaction" );
         ObjectIdCollection ids = btr.GetBlockReferenceIds( directOnly, true );
         int cnt = ids.Count;
         for( int i = 0; i < cnt; i++ )
         {
            yield return tr.GetObject<BlockReference>(ids[i], mode);
         }
         if( btr.IsDynamicBlock )
         {
            BlockTableRecord btr2 = null;
            ObjectIdCollection blockIds = btr.GetAnonymousBlockIds();
            cnt = blockIds.Count;
            for( int i = 0; i < cnt; i++ )
            {
               btr2 = tr.GetObject<BlockTableRecord>(blockIds[i]);
               ids = btr2.GetBlockReferenceIds( directOnly, true );
               int cnt2 = ids.Count;
               for( int j = 0; j < cnt2; j++ )
               {
                  yield return tr.GetObject<BlockReference>( ids[j], mode);
               }
            }
         }
      }

      public static bool IsA<T>(this ObjectId id, bool exact = false) where T: DBObject
      {
         return exact ? id.ObjectClass == RXClass<T>.Value 
            : id.ObjectClass.IsDerivedFrom(RXClass<T>.Value);
      }
   }

   /// <summary>
   /// A class that caches the result of RXObject.GetClass() (which must query
   /// its Type argument for existence of a DisposableWrapperAttribute, making 
   /// it terribly-ineffecient in a high-frequency usage scenario).
   /// 
   /// By using the managed type as the generic argument, a seperate generic type 
   /// can be constructed for each managed type used with RXClass<T>, essentially
   /// reducing the lookup of the RXClass to little more than accessing a field of 
   /// a static type. You can equate it to a static field of a static type that
   /// is initialized to the result of RXObject.GetClass(type), reqiring a distinct
   /// field for each type argument.
   ///
   /// When using this class verses RXObject.GetClass(), the latter must be called
   /// only once to initialize the static field, and from that point on, the field's
   /// value is used. This class is only useful in cases where the managed wrapper 
   /// type argument is a design-time constant, a fairly-common pattern:
   /// 
   ///    RXClass rxclass = RXObject.GetClass(typeof(ManagedWrapperType));
   ///    
   /// By replacing the above with:
   /// 
   ///    RXClass rxclass = RXClass<ManagedWrapperType>.Value;
   ///    
   /// The assignment to the variable is nothing more than referencing
   /// the Value field of the RXClass<T> type for the given generic 
   /// argument. That allows a reference to the Value field to replace 
   /// the need for the variable as well.
   ///    
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public static class RXClass<T> where T: RXObject
   {
      public static readonly RXClass Value = RXObject.GetClass(typeof(T));
   }

   public static class AutoNumCommands
   {
      [CommandMethod( "AUTONUM" )]
      public static void AutoNumExample()
      {
         try
         {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if( overrule != null )
            {
               var peo = new PromptIntegerOptions( "\nNew seed value: " );
               peo.UseDefaultValue = true;
               peo.DefaultValue = overrule.Next;
               peo.AllowNegative = false;
               peo.AllowZero = false;
               peo.LowerLimit = overrule.Next;
               while( true )
               {
                  PromptIntegerResult pir = doc.Editor.GetInteger( peo );
                  if( pir.Status != PromptStatus.OK )
                     return;
                  if( pir.Value < overrule.Next )
                  {
                     doc.Editor.WriteMessage( "\nValue cannot be less than {0},", overrule.Next );
                     continue;
                  }
                  if( pir.Value != overrule.Next )
                  {
                     overrule.Next = pir.Value;
                     doc.Editor.WriteMessage( "\nNew seed value: {0}", overrule.Next );
                  }
                  return;
               }
            }
            var pneo = new PromptNestedEntityOptions( "\nSelect attribute: " );
            var pner = doc.Editor.GetNestedEntity( pneo );
            if( pner.Status != PromptStatus.OK )
               return;
            if( !pner.ObjectId.IsA<AttributeReference>())
            {
               doc.Editor.WriteMessage( "\nInvalid selection, must select a block attribute" );
               return;
            }
            ObjectId blockId = ObjectId.Null;
            string tag = string.Empty;
            string name = string.Empty;
            using( Transaction tr = new OpenCloseTransaction() )
            {
               var attref = tr.GetObject<AttributeReference>(pner.ObjectId);
               tag = attref.Tag.ToUpper();
               var blkref = tr.GetObject<BlockReference>(attref.OwnerId);
               blockId = blkref.DynamicBlockTableRecord;
               name = tr.GetObject<BlockTableRecord>(blockId).Name;
               tr.Commit();
            }
            doc.Editor.WriteMessage( "\nScanning existing attributes..." );
            overrule = new AutoNumberAttributeOverrule( blockId, tag );
            doc.Editor.WriteMessage( "   done." );
            int next = overrule.Next;
            doc.Editor.WriteMessage( "\nAuto-numbering of {0}.{1} enabled,", name, tag );
            doc.Editor.WriteMessage( "\nNext available value: {0}", next );
            overrule.Enabled = true;
         }
         catch( System.Exception ex )
         {
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage( ex.ToString() );
            throw ex;
         }
      }

      static AutoNumCommands()
      {
         Application.QuitWillStart += quitWillStart;
      }

      static void quitWillStart( object sender, EventArgs e )
      {
         Application.QuitWillStart -= quitWillStart;
         if( overrule != null )
         {
            overrule.Dispose();
            overrule = null;
         }
      }

      static AutoNumberAttributeOverrule overrule = null;
   }
  
}

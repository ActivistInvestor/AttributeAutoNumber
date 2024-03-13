/// AutoNumberAttributeOverrule.cs  2011  Tony Tanzillo
/// Distributed under the terms of the MIT license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.MacroRecorder;
using System.Runtime.CompilerServices;

namespace Autodesk.AutoCAD.DatabaseServices.MyExtensions
{
   /// <summary>
   ///
   /// Disclaimer:
   ///
   /// This code is intended for demonstration purposes only,
   /// and as-provided, is not necessarily fit or suitable for
   /// any specific purpose.
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
   /// by way of operations like INSERT (from block or file),
   /// DXFIN, etc.
   ///
   /// To use the sample, you need a block with at least one
   /// attribute. With at least one insertion of the block in
   /// the current drawing, issue the AUTONUM command, and
   /// select the attribute whose value is to be managed.
   ///
   /// The demonstration uses block attributes mainly for the
   /// purpose of making the behavior easily observable, but
   /// the same concepts and approach used can be applied to
   /// data stored in Xdata or Xrecords as well.
   ///
   /// When using this approach for ensuring unique values in
   /// xdata or in an extension dictionary, you would use an
   /// Overrule with an XData filter or Dictionary filter, to
   /// minimize overhead, which may be the only thing that may
   /// make the use of an ObjectOverrule feasable, considering
   /// the high overhead of an unconstrained ObjectOverrule.
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
         if( string.IsNullOrEmpty( tag ) )
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
         using(Transaction tr = blockId.Database.TransactionManager.StartTransaction())
         {
            // From all insertions of the given block, get the numerically-greatest
            // TextString value of the AttributeReference with the given tag, as an
            // integer:

            BlockTableRecord btr = tr.GetObject<BlockTableRecord>(blockId);
            result = btr.GetAttributeReferences(tr, tag).Select(GetNumericValue).Max();
            tr.Commit();
         }
         return result + 1;
      }

      static int GetNumericValue( AttributeReference attref )
      {
         int num = 1;
         int.TryParse( attref.TextString, out num );
         return num;
      }

      public override void Close( DBObject obj )
      {
         if(obj.Database == this.db && obj.IsNewObject && obj.IsWriteEnabled )
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
   /// Excerpted from AcDbLinq.cs
   /// </summary>

   internal static class AcDbExtensionMethods
   {

      public static T GetObject<T>( this Transaction tr, ObjectId id, OpenMode mode = OpenMode.ForRead )
         where T : DBObject
      {
         return (T) tr.GetObject(id, mode, false, false);
      }

      /// <summary>
      /// Get all AttributeReferences with the given tag, from
      /// every insertion of the given block.
      /// </summary>

      public static IEnumerable<AttributeReference> GetAttributeReferences( this BlockTableRecord btr, Transaction tr, string tag )
      {
         if( btr == null )
            throw new ArgumentNullException( "btr" );
         tr = tr ?? btr.Database.TransactionManager.TopTransaction;
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
         tr = tr ?? blkref.Database.TransactionManager.TopTransaction;
         if( tr == null )
            throw new InvalidOperationException( "No transaction" );
         foreach( ObjectId id in blkref.AttributeCollection )
            yield return (AttributeReference) tr.GetObject( id, mode, false, false );
      }

      /// <summary>
      /// Get all references to the given BlockTableRecord,
      /// including dynamic block references.
      /// </summary>

      public static IEnumerable<BlockReference> GetBlockReferences( this BlockTableRecord btr, Transaction tr, OpenMode mode = OpenMode.ForRead, bool directOnly = true )
      {
         if( btr == null )
            throw new ArgumentNullException( "btr" );
         tr = tr ?? btr.Database.TransactionManager.TopTransaction;
         if( tr == null )
            throw new InvalidOperationException( "No transaction" );
         ObjectIdCollection ids = btr.GetBlockReferenceIds( directOnly, true );
         int cnt = ids.Count;
         for( int i = 0; i < cnt; i++ )
         {
            yield return (BlockReference) tr.GetObject( ids[i], mode, false, false );
         }
         if( btr.IsDynamicBlock )
         {
            BlockTableRecord btr2 = null;
            ObjectIdCollection blockIds = btr.GetAnonymousBlockIds();
            cnt = blockIds.Count;
            for( int i = 0; i < cnt; i++ )
            {
               btr2 = tr.GetObject<BlockTableRecord>( blockIds[i], OpenMode.ForRead);
               ids = btr2.GetBlockReferenceIds( directOnly, true );
               int cnt2 = ids.Count;
               for( int j = 0; j < cnt2; j++ )
               {
                  yield return tr.GetObject<BlockReference>( ids[j], mode);
               }
            }
         }
      }
   }

   abstract class ObjectOverrule<T> : ObjectOverrule where T : DBObject
   {
      bool enabled = false;
      static RXClass rxclass = RXClass.GetClass( typeof( T ) );

      public ObjectOverrule( bool enabled = true )
      {
         Enabled = enabled;
      }

      public virtual bool Enabled
      {
         get
         {
            return this.enabled;
         }
         set
         {
            if( this.enabled ^ value )
            {
               this.enabled = value;
               if( value )
                  Overrule.AddOverrule( rxclass, this, true );
               else
                  Overrule.RemoveOverrule( rxclass, this );
               OnEnabledChanged( this.enabled );
            }
         }
      }

      protected virtual void OnEnabledChanged( bool enabled )
      {
      }

      protected override void Dispose( bool disposing )
      {
         if( disposing && ! base.IsDisposed )
            Enabled = false;
         base.Dispose( disposing );
      }
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
            if( !pner.ObjectId.ObjectClass.IsDerivedFrom( RXClass.GetClass( typeof( AttributeReference ) ) ) )
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
            doc.Editor.WriteMessage( "\nScanning existing attribute values..." );
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

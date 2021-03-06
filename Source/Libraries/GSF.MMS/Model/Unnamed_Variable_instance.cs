//
// This file was generated by the BinaryNotes compiler.
// See http://bnotes.sourceforge.net 
// Any modifications to this file will be lost upon recompilation of the source ASN.1. 
//

using GSF.ASN1;
using GSF.ASN1.Attributes;
using GSF.ASN1.Coders;

namespace GSF.MMS.Model
{
    
    [ASN1PreparedElement]
    [ASN1Sequence(Name = "Unnamed_Variable_instance", IsSet = false)]
    public class Unnamed_Variable_instance : IASN1PreparedElement
    {
        private static readonly IASN1PreparedElementData preparedData = CoderFactory.getInstance().newPreparedElementData(typeof(Unnamed_Variable_instance));
        private Access_Control_List_instance accessControl_;
        private Address address_;
        private TypeDescription typeDescription_;

        [ASN1Element(Name = "address", IsOptional = false, HasTag = true, Tag = 0, HasDefaultValue = false)]
        public Address Address
        {
            get
            {
                return address_;
            }
            set
            {
                address_ = value;
            }
        }


        [ASN1Element(Name = "accessControl", IsOptional = false, HasTag = true, Tag = 1, HasDefaultValue = false)]
        public Access_Control_List_instance AccessControl
        {
            get
            {
                return accessControl_;
            }
            set
            {
                accessControl_ = value;
            }
        }


        [ASN1Element(Name = "typeDescription", IsOptional = false, HasTag = true, Tag = 2, HasDefaultValue = false)]
        public TypeDescription TypeDescription
        {
            get
            {
                return typeDescription_;
            }
            set
            {
                typeDescription_ = value;
            }
        }


        public void initWithDefaults()
        {
        }


        public IASN1PreparedElementData PreparedData
        {
            get
            {
                return preparedData;
            }
        }
    }
}